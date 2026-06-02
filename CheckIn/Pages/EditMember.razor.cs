using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class EditMember
{
    [Parameter] public Guid MemberId { get; set; }
    
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private MudForm _form = null!;
    private bool _success;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private MemberRegistrationDto? _memberDto;
    private DateTime? _dob;

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanAddUsers)
        {
            await LoadMember();
        }
    }

    private async Task ShowQrCode()
    {
        if (_memberDto == null) return;
        
        var parameters = new DialogParameters 
        { 
            ["MemberId"] = MemberId,
            ["Username"] = _memberDto.Username, 
            ["QrSecret"] = _memberDto.QrSecret 
        };
        var options = new DialogOptions { CloseOnEscapeKey = true };
        await DialogService.ShowAsync<CheckIn.Components.MemberQrCodeDialog>("Member Login QR", parameters, options);
    }

    private async Task LoadMember()
    {
        _isLoading = true;
        var response = await HttpClient.GetAsync($"/api/members/{MemberId}");
        if (response.IsSuccessStatusCode)
        {
            _memberDto = await response.Content.ReadFromJsonAsync<MemberRegistrationDto>();
            if (_memberDto != null)
            {
                _dob = _memberDto.DateOfBirth.ToDateTime(TimeOnly.MinValue);
                _memberDto.Password = "********";
            }
        }
        else
        {
            Snackbar.Add("Failed to load member data", Severity.Error);
        }
        _isLoading = false;
    }

    private async Task Save()
    {
        if (_memberDto == null) return;
        
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        _isSaving = true;
        _memberDto.DateOfBirth = DateOnly.FromDateTime(_dob ?? DateTime.Today);
        
        var response = await HttpClient.PutAsJsonAsync($"/api/members/{MemberId}", _memberDto);

        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Profile updated successfully", Severity.Success);
            NavigationManager.NavigateTo("/find-member");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Snackbar.Add(error ?? "Failed to update profile", Severity.Error);
        }
        _isSaving = false;
    }
}