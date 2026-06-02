using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class RegisterMember
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private MudForm _form = null!;
    private bool _success;
    private string? _serverError;
    private Member? _registeredMember;
    private MemberRegistrationDto _newMember = new();
    private DateTime? _dob = DateTime.Today;

    private async Task Register(bool thenCheckIn)
    {
        _serverError = null;
        _registeredMember = null;
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        _newMember.DateOfBirth = DateOnly.FromDateTime(_dob ?? DateTime.Today);
        var response = await HttpClient.PostAsJsonAsync("/api/members", _newMember);

        if (response.IsSuccessStatusCode)
        {
            var member = await response.Content.ReadFromJsonAsync<Member>();
            if (member != null)
            {
                if (thenCheckIn)
                {
                    var checkInResponse = await HttpClient.PostAsJsonAsync("/api/attendance/check-in", new { memberId = member.MemberId });
                    if (checkInResponse.IsSuccessStatusCode)
                    {
                        Snackbar.Add($"Registered and Checked in {member.FirstName}", Severity.Success);
                        NavigationManager.NavigateTo("/RecordView");
                    }
                    else
                    {
                        Snackbar.Add($"Registered {member.FirstName} but Check-in failed", Severity.Warning);
                        NavigationManager.NavigateTo("/find-member");
                    }
                }
                else
                {
                    _registeredMember = member;
                    Snackbar.Add($"Registered {member.FirstName}", Severity.Success);
                    ResetForm();
                }
            }
        }
        else
        {
            _serverError = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(_serverError)) _serverError = "Registration failed. Please check the inputs.";
            Snackbar.Add("Registration failed", Severity.Error);
        }
    }

    private void ResetForm()
    {
        _newMember = new();
        _dob = DateTime.Today;
    }

    private async Task ShowRegisteredQrCode()
    {
        if (_registeredMember == null) return;
        
        var parameters = new DialogParameters 
        { 
            ["MemberId"] = _registeredMember.MemberId,
            ["Username"] = _registeredMember.Username, 
            ["QrSecret"] = _registeredMember.QrSecret 
        };
        var options = new DialogOptions { CloseOnEscapeKey = true };
        await DialogService.ShowAsync<CheckIn.Components.MemberQrCodeDialog>("Member Login QR", parameters, options);
    }
}
