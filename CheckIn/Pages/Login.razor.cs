using System.Threading.Tasks;
using BlazorCameraStreamer;
using CheckIn.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class Login
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private CameraStreamer? _cameraStreamer;
    private string _username = "";
    private string _password = "";
    private bool _isLoggingIn = false;

    private async Task OnLogin()
    {
        if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
        {
            Snackbar.Add("Please enter both username and password.", Severity.Warning);
            return;
        }

        _isLoggingIn = true;
        if (await AuthService.LoginAsync(_username, _password))
        {
            if (AuthService.MustChangePassword)
            {
                var options = new DialogOptions { CloseOnEscapeKey = false, BackdropClick = false };
                var dialog = await DialogService.ShowAsync<CheckIn.Components.ForcePasswordResetDialog>("Update Your Password", options);
                var result = await dialog.Result;
                
                if (result == null || result.Canceled)
                {
                    AuthService.Logout();
                    _isLoggingIn = false;
                    return;
                }
            }

            Snackbar.Add("Login successful", Severity.Success);
            NavigationManager.NavigateTo("/");
        }
        else
        {
            Snackbar.Add("Login failed. Please check your credentials.", Severity.Error);
        }
        _isLoggingIn = false;
    }

    private async Task OpenForgotPasswordDialog()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        await DialogService.ShowAsync<CheckIn.Components.ForgotPasswordDialog>("Forgotten Password", options);
    }
}