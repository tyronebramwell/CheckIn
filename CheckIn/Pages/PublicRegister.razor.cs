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

public partial class PublicRegister
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private MudForm _form = null!;
    private bool _success;
    private bool _isProcessing = false;
    private string? _serverError;
    private MemberRegistrationDto _newMember = new();
    private DateTime? _dob = DateTime.Today;

    private async Task RegisterAndCheckIn()
    {
        _serverError = null;
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        _isProcessing = true;
        _newMember.DateOfBirth = DateOnly.FromDateTime(_dob ?? DateTime.Today);
        
        var response = await HttpClient.PostAsJsonAsync("/api/members/public", _newMember);

        if (response.IsSuccessStatusCode)
        {
            var member = await response.Content.ReadFromJsonAsync<Member>();
            if (member != null)
            {
                if (await AuthService.LoginAsync(_newMember.Username, _newMember.Password))
                {
                    var checkInResponse = await HttpClient.PostAsync("/api/attendance/self-check-in", null);
                    if (checkInResponse.IsSuccessStatusCode)
                    {
                        Snackbar.Add("Welcome! You are registered and checked in.", Severity.Success);
                        NavigationManager.NavigateTo("/");
                    }
                }
            }
        }
        else
        {
            _serverError = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(_serverError)) _serverError = "Registration failed. Please try again.";
        }
        _isProcessing = false;
    }
}
