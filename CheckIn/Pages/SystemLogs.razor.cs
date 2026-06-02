using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class SystemLogs
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<SystemLog> _logs = new();

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanManageVolunteers)
        {
            await LoadLogs();
        }
    }

    private async Task LoadLogs()
    {
        var response = await HttpClient.GetAsync("/api/logs");
        if (response.IsSuccessStatusCode)
        {
            _logs = await response.Content.ReadFromJsonAsync<List<SystemLog>>() ?? new();
        }
    }
}
