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

public partial class AttendanceHistory
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<AttendanceHistoryDto> _historyLogs = new();
    private DateTime? _selectedDate = DateTime.Today;

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanViewData)
        {
            await LoadHistory();
        }
    }

    private async Task OnDateChanged(DateTime? newDate)
    {
        _selectedDate = newDate;
        await LoadHistory();
    }

    private async Task LoadHistory()
    {
        var dateQuery = _selectedDate.HasValue ? $"?date={_selectedDate.Value:yyyy-MM-dd}" : "";
        var response = await HttpClient.GetAsync($"/api/attendance/history{dateQuery}");
        if (response.IsSuccessStatusCode)
        {
            _historyLogs = await response.Content.ReadFromJsonAsync<List<AttendanceHistoryDto>>() ?? new();
        }
    }

    private void ViewProfile(Guid memberId)
    {
        NavigationManager.NavigateTo($"/edit-member/{memberId}");
    }

    private async Task ExportToCsv()
    {
        var dateQuery = _selectedDate.HasValue ? $"?date={_selectedDate.Value:yyyy-MM-dd}" : "";
        var response = await HttpClient.GetAsync($"/api/attendance/export{dateQuery}");
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ExportResponse>();
            Snackbar.Add($"Exported {result?.recordCount} records to {result?.fileName}", Severity.Success);
        }
        else
        {
            Snackbar.Add("Export failed.", Severity.Error);
        }
    }
}

public record ExportResponse(string fileName, string filePath, int recordCount);

public record AttendanceHistoryDto(
    Guid LogId,
    Guid MemberId,
    string FirstName,
    string LastName,
    DateTime CheckInTime,
    DateTime? CheckOutTime,
    string Guardian1Phone,
    string EventName);
