using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorCameraStreamer;
using CheckIn.Components;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace CheckIn.Pages;

public partial class Home : IDisposable
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private CameraStreamer? _cameraStreamer;
    private bool _isMemberCheckedIn = false;
    private bool _showSuccessMessage = false;
    private string _successActionText = "";
    private int _countdown = 15;
    private bool _actionTaken = false;
    private bool _allowPublicRegistration = false;
    
    private string _actionResultClass = "";
    private string _actionMessage = "";
    private string _actionMemberName = "";
    private bool _isProcessingQrAction = false;
    private bool _isScanning = false;
    private bool _isProcessingManualAction = false;
    private bool _cameraSupported = false;
    private bool _isCheckingCamera = true;
    private bool _isCameraEnabled = false;
    private string? _currentDeviceId;
    private System.Timers.Timer? _qrScanTimer;

    protected override async Task OnInitializedAsync()
    {
        _ = LoadConfig(); // Fire and forget
        
        if (AuthService.IsLoggedIn && AuthService.IsMember)
        {
            await GetAttendanceStatus();
            _ = StartInactivityTimer();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (!AuthService.IsLoggedIn || AuthService.IsMember))
        {
            await CheckCameraSupport();
            
            if (_cameraSupported && !AuthService.IsLoggedIn)
            {
                StartQrScanning();
            }
        }
    }

    private async Task CheckCameraSupport()
    {
        try 
        {
            _cameraSupported = await JSRuntime.InvokeAsync<bool>("checkCameraSupport");
        }
        catch 
        {
            _cameraSupported = false;
        }
        finally
        {
            _isCheckingCamera = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OpenCameraSelection()
    {
        if (_cameraStreamer == null) return;
        
        try
        {
            var cameras = await _cameraStreamer.GetCameraDevicesAsync();
            var parameters = new DialogParameters 
            { 
                ["Cameras"] = cameras,
                ["CurrentDeviceId"] = _currentDeviceId,
                ["IsEnabled"] = _isCameraEnabled
            };
            var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
            
            var dialog = await DialogService.ShowAsync<CameraSelectionDialog>("Camera Settings", parameters, options);
            var result = await dialog.Result;
            
            if (result != null && !result.Canceled && result.Data is CameraSelectionDialog.CameraSelectionResult selection)
            {
                _isCameraEnabled = selection.IsEnabled;
                _currentDeviceId = selection.DeviceId;

                if (_isCameraEnabled)
                {
                    if (!string.IsNullOrEmpty(_currentDeviceId))
                    {
                        await _cameraStreamer.ChangeCameraAsync(_currentDeviceId);
                    }
                    else
                    {
                        await _cameraStreamer.StartAsync();
                    }
                }
                else
                {
                    await _cameraStreamer.StopAsync();
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch
        {
            Snackbar.Add("Could not access camera list. Ensure you are on HTTPS.", Severity.Error);
        }
    }

    private void StartQrScanning()
    {
        _qrScanTimer = new System.Timers.Timer(300); 
        _qrScanTimer.Elapsed += async (s, e) => await ScanForQrCode();
        _qrScanTimer.Start();
    }

    private async Task ScanForQrCode()
    {
        if (!_isCameraEnabled || _isScanning || _isProcessingQrAction || AuthService.IsLoggedIn) return;
        _isScanning = true;

        try {
            var result = await JSRuntime.InvokeAsync<string>("qrHelper.decode");
            
            if (!string.IsNullOrEmpty(result)) {
                if (!_isProcessingQrAction)
                {
                    var parts = result.Split('|');
                    if (parts.Length == 2 && Guid.TryParse(parts[1], out var secret)) {
                        _isProcessingQrAction = true;
                        _qrScanTimer?.Stop(); // Hard stop while processing
                        await HandleDirectQrAction(parts[0], secret);
                        _qrScanTimer?.Start();
                    }
                }
            }
            else
            {
                if (_isProcessingQrAction && string.IsNullOrEmpty(_actionMessage))
                {
                    _isProcessingQrAction = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
        } 
        catch { /* Fail silently */ }
        finally { _isScanning = false; }
    }

    private async Task HandleDirectQrAction(string scannedUsername, Guid secret, Guid? eventId = null)
    {
        var url = eventId.HasValue ? $"/api/attendance/qr-action?eventId={eventId}" : "/api/attendance/qr-action";
        var response = await HttpClient.PostAsJsonAsync(url, new { username = scannedUsername, qrSecret = secret });

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<QrConflictResponse>();
            if (conflict != null && conflict.Events != null)
            {
                var parameters = new DialogParameters { ["Events"] = conflict.Events };
                var options = new DialogOptions { CloseOnEscapeKey = true, BackdropClick = false };
                var dialog = await DialogService.ShowAsync<EventSelectionDialog>("Multiple Events", parameters, options);
                var result = await dialog.Result;

                if (result != null && !result.Canceled && result.Data is Guid selectedEventId)
                {
                    await HandleDirectQrAction(scannedUsername, secret, selectedEventId);
                }
                else
                {
                    _isProcessingQrAction = false;
                    await InvokeAsync(StateHasChanged);
                }
                return;
            }
        }

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<QrActionData>();
            if (data != null)
            {
                _actionMemberName = !string.IsNullOrEmpty(data.Username) ? data.Username : scannedUsername;
                
                if (data.Action == "CheckedIn")
                {
                    _actionResultClass = "success";
                    _actionMessage = "is now Checked In!";
                }
                else
                {
                    _actionResultClass = "error";
                    _actionMessage = "is now Checked Out!";
                }

                await InvokeAsync(StateHasChanged);
                
                await Task.Delay(6000);
                
                _actionResultClass = "";
                _actionMessage = "";
                _actionMemberName = "";
                
                // Allow a small buffer before re-enabling scanning
                await Task.Delay(500);
                _isProcessingQrAction = false;
                await InvokeAsync(StateHasChanged);
            }
        }
        else
        {
            _isProcessingQrAction = false;
        }
    }

    private async Task LoadConfig()
    {
        try 
        {
            var config = await HttpClient.GetFromJsonAsync<ConfigResponse>("/api/config");
            _allowPublicRegistration = config?.AllowPublic ?? false;
            await InvokeAsync(StateHasChanged);
        }
        catch { /* Fallback to false */ }
    }

    private async Task StartInactivityTimer()
    {
        while (_countdown > 0 && !_actionTaken)
        {
            await Task.Delay(1000);
            if (!_actionTaken)
            {
                _countdown--;
                StateHasChanged();
            }
        }

        if (!_actionTaken)
        {
            LogoutNow();
        }
    }

    private async Task OpenChangePasswordDialog()
    {
        _actionTaken = true; // Pause auto-logout while dialog is open
        
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MemberChangePasswordDialog>("Change Your Password", options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
             LogoutNow(); // Log them out for safety after password change
        }
        else
        {
            _actionTaken = false; // Resume timer
            _countdown = 15;
            _ = StartInactivityTimer();
        }
    }

    private async Task GetAttendanceStatus()
    {
        var response = await HttpClient.GetFromJsonAsync<StatusResponse>("/api/attendance/self-status");
        if (response != null)
        {
            _isMemberCheckedIn = response.IsActive;
        }
    }

    private async Task SelfCheckIn(Guid? eventId = null)
    {
        if (_isProcessingManualAction) return;
        _isProcessingManualAction = true;
        _actionTaken = true;

        try
        {
            var url = eventId.HasValue ? $"/api/attendance/self-check-in?eventId={eventId}" : "/api/attendance/self-check-in";
            var response = await HttpClient.PostAsync(url, null);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var conflict = await response.Content.ReadFromJsonAsync<InactivityConflictResponse>();
                if (conflict != null && conflict.Events != null)
                {
                    var parameters = new DialogParameters { ["Events"] = conflict.Events };
                    var options = new DialogOptions { CloseOnEscapeKey = true, BackdropClick = false };
                    var dialog = await DialogService.ShowAsync<EventSelectionDialog>("Select Event", parameters, options);
                    var result = await dialog.Result;

                    if (result != null && !result.Canceled && result.Data is Guid selectedEventId)
                    {
                        _isProcessingManualAction = false; // Reset to allow the recursive call
                        await SelfCheckIn(selectedEventId);
                    }
                    else
                    {
                        _actionTaken = false;
                        _countdown = 15;
                        _ = StartInactivityTimer();
                    }
                    return;
                }
            }

            if (response.IsSuccessStatusCode)
            {
                _successActionText = "checked in";
                await StartPostActionCountdown();
            }
            else
            {
                _actionTaken = false;
                Snackbar.Add("Check-in failed. Are you already checked in?", Severity.Warning);
            }
        }
        finally
        {
            _isProcessingManualAction = false;
        }
    }

    private async Task SelfCheckOut()
    {
        _actionTaken = true;
        var response = await HttpClient.PostAsync("/api/attendance/self-check-out", null);
        if (response.IsSuccessStatusCode)
        {
            _successActionText = "checking out";
            await StartPostActionCountdown();
        }
        else
        {
            _actionTaken = false;
            var error = await response.Content.ReadAsStringAsync();
            Snackbar.Add(error ?? "Check-out failed.", Severity.Error);
        }
    }

    private async Task StartPostActionCountdown()
    {
        _showSuccessMessage = true;
        _countdown = 5;
        StateHasChanged();

        while (_countdown > 0)
        {
            await Task.Delay(1000);
            _countdown--;
            StateHasChanged();
        }

        LogoutNow();
    }

    private void LogoutNow()
    {
        _actionTaken = true;
        AuthService.Logout();
        NavigationManager.NavigateTo("/");
    }

    public void Dispose()
    {
        _qrScanTimer?.Stop();
        _qrScanTimer?.Dispose();
    }
}

public record StatusResponse(bool IsActive);
public record ConfigResponse(bool AllowPublic);
public class QrActionData
{
    public string Action { get; set; } = "";
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public record QrConflictResponse(string Action, List<Event> Events);
public record InactivityConflictResponse(string Message, List<Event> Events);
