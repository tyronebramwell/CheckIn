using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class RecordView
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<ActiveAttendanceDto> _activeLogs = new();

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanViewData)
        {
            await LoadActiveAttendance();
        }
    }

    private async Task LoadActiveAttendance()
    {
        var response = await HttpClient.GetAsync("/api/attendance/active");
        if (response.IsSuccessStatusCode)
        {
            _activeLogs = await response.Content.ReadFromJsonAsync<List<ActiveAttendanceDto>>() ?? new();
        }
    }

    private void ViewProfile(Guid memberId)
    {
        NavigationManager.NavigateTo($"/edit-member/{memberId}");
    }

    private async Task CheckOut(ActiveAttendanceDto log)
    {
        var response = await HttpClient.PutAsJsonAsync("/api/attendance/check-out", new { logId = log.LogId });
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add($"Checked out {log.FirstName}", Severity.Success);
            await LoadActiveAttendance();
        }
        else
        {
            Snackbar.Add("Check-out failed", Severity.Error);
        }
    }
}

public record ActiveAttendanceDto(
    Guid LogId,
    Guid MemberId,
    string FirstName,
    string LastName,
    string? Notes,
    string Guardian1Phone,
    string? Guardian2Phone,
    DateTime CheckInTime,
    string EventName);
