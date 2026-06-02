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

public partial class MemberRoster
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<Member> _members = new();

    protected override async Task OnInitializedAsync()
    {
        if (AuthService.IsLoggedIn && AuthService.CanViewData)
        {
            await LoadMembers();
        }
    }

    private async Task LoadMembers()
    {
        var response = await HttpClient.GetAsync("/api/members");
        if (response.IsSuccessStatusCode)
        {
            _members = await response.Content.ReadFromJsonAsync<List<Member>>() ?? new();
        }
    }

    private void ViewProfile(Guid memberId)
    {
        NavigationManager.NavigateTo($"/edit-member/{memberId}");
    }

    private async Task ShowQrCode(Member member)
    {
        var parameters = new DialogParameters 
        { 
            ["MemberId"] = member.MemberId,
            ["Username"] = member.Username, 
            ["QrSecret"] = member.QrSecret 
        };
        var options = new DialogOptions { CloseOnEscapeKey = true };
        await DialogService.ShowAsync<CheckIn.Components.MemberQrCodeDialog>("Member Login QR", parameters, options);
    }

    private async Task ExportToCsv()
    {
        var response = await HttpClient.GetAsync("/api/members/export");
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<MemberRosterExportResponse>();
            Snackbar.Add($"Exported all members to {result?.fileName}", Severity.Success);
        }
        else
        {
            Snackbar.Add("Export failed.", Severity.Error);
        }
    }
}

public record MemberRosterExportResponse(string fileName, string filePath);
