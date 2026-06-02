using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Components;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class VolunteerManagement
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<VolunteerSummary> _volunteers = new();

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanManageVolunteers)
        {
            await LoadVolunteers();
        }
    }

    private async Task LoadVolunteers()
    {
        var response = await HttpClient.GetAsync("/api/volunteers");
        if (response.IsSuccessStatusCode)
        {
            _volunteers = await response.Content.ReadFromJsonAsync<List<VolunteerSummary>>() ?? new();
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters();
        var dialog = await DialogService.ShowAsync<CreateVolunteerDialog>("Add Volunteer", parameters);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            await LoadVolunteers();
        }
    }

    private async Task OpenEditDialog(VolunteerSummary volunteer)
    {
        var parameters = new DialogParameters { ["Volunteer"] = volunteer };
        var dialog = await DialogService.ShowAsync<EditVolunteerDialog>("Edit Permissions", parameters);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            await LoadVolunteers();
        }
    }

    private async Task OpenPasswordDialog(VolunteerSummary volunteer)
    {
        var parameters = new DialogParameters { ["VolunteerId"] = volunteer.VolunteerId };
        var dialog = await DialogService.ShowAsync<UpdatePasswordDialog>("Change Password", parameters);
        await dialog.Result;
    }

    private async Task DeleteVolunteer(VolunteerSummary volunteer)
    {
        bool? confirmed = await DialogService.ShowMessageBoxAsync("Delete", $"Are you sure you want to delete {volunteer.Username}?", yesText: "Delete", cancelText: "Cancel");
        if (confirmed == true)

        {
            var response = await HttpClient.DeleteAsync($"/api/volunteers/{volunteer.VolunteerId}");
            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("Volunteer deleted", Severity.Success);
                await LoadVolunteers();
            }
            else
            {
                Snackbar.Add("Delete failed", Severity.Error);
            }
        }
    }
}

public record VolunteerSummary(Guid VolunteerId, string Username, string? Email, bool CanViewData, bool CanAddUsers, bool CanManageVolunteers, bool CanManageEvents);
