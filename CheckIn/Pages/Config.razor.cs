using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class Config
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;

    private bool _isLoading = true;
    private bool _isSaving = false;
    private bool _allowPublic = false;
    private string _orgName = "Charity Check-In";
    private string _smtpHost = "smtp.gmail.com";
    private string _smtpPort = "587";
    private string _smtpUser = "";
    private string _smtpPass = "";
    private int _csvInterval = 5;
    private List<SystemConfig> _settings = new();

    protected override async Task OnInitializedAsync()
    {
        if (!AuthService.IsLoggedIn || !AuthService.CanManageVolunteers)
        {
            // Unauthorized access check (AuthService handles most of this)
            return;
        }

        await LoadSettings();
    }

    private async Task LoadSettings()
    {
        _isLoading = true;
        try
        {
            _settings = await HttpClient.GetFromJsonAsync<List<SystemConfig>>("/api/config/admin") ?? new();
            
            var publicReg = _settings.FirstOrDefault(s => s.Key == "ALLOW_PUBLIC_REGISTRATION");
            if (publicReg != null) _allowPublic = bool.Parse(publicReg.Value);

            var org = _settings.FirstOrDefault(s => s.Key == "ORG_NAME");
            if (org != null) _orgName = org.Value;

            var host = _settings.FirstOrDefault(s => s.Key == "SMTP_HOST");
            if (host != null) _smtpHost = host.Value;

            var port = _settings.FirstOrDefault(s => s.Key == "SMTP_PORT");
            if (port != null) _smtpPort = port.Value;

            var user = _settings.FirstOrDefault(s => s.Key == "SMTP_USER");
            if (user != null) _smtpUser = user.Value;

            var pass = _settings.FirstOrDefault(s => s.Key == "SMTP_PASS");
            if (pass != null) _smtpPass = pass.Value;

            var interval = _settings.FirstOrDefault(s => s.Key == "CSV_SYNC_INTERVAL_MINS");
            if (interval != null && int.TryParse(interval.Value, out int iv)) _csvInterval = iv;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading settings: {ex.Message}", Severity.Error);
        }
        _isLoading = false;
    }

    private void OnAllowPublicChanged(bool val)
    {
        _allowPublic = val;
    }

    private async Task SaveSettings()
    {
        _isSaving = true;
        try
        {
            var updateList = new List<SystemConfig>
            {
                new SystemConfig { Key = "ALLOW_PUBLIC_REGISTRATION", Value = _allowPublic.ToString() },
                new SystemConfig { Key = "ORG_NAME", Value = _orgName },
                new SystemConfig { Key = "SMTP_HOST", Value = _smtpHost },
                new SystemConfig { Key = "SMTP_PORT", Value = _smtpPort },
                new SystemConfig { Key = "SMTP_USER", Value = _smtpUser },
                new SystemConfig { Key = "SMTP_PASS", Value = _smtpPass },
                new SystemConfig { Key = "CSV_SYNC_INTERVAL_MINS", Value = _csvInterval.ToString() }
            };

            var response = await HttpClient.PutAsJsonAsync("/api/config", updateList);
            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("Settings saved successfully.", Severity.Success);
            }
            else
            {
                Snackbar.Add("Failed to save settings.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving settings: {ex.Message}", Severity.Error);
        }
        _isSaving = false;
    }

    private async Task SyncCsvs()
    {
        var response = await HttpClient.PostAsync("/api/config/sync-now", null);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("CSV files synchronized successfully.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to synchronize CSV files.", Severity.Error);
        }
    }
}