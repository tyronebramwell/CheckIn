using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CheckIn.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Pages;

public partial class FindMember
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private string _searchQuery = "";
    private List<Member> _searchResults = new();
    private Member? _selectedMember;
    private bool _isSearching = false;

    private async Task OnSearchChanged(string query)
    {
        _searchQuery = query;
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults.Clear();
            _selectedMember = null;
            return;
        }
        await SearchMembers();
    }

    private async Task SearchMembers()
    {
        _isSearching = true;
        var response = await HttpClient.GetAsync($"/api/members?search={_searchQuery}");
        if (response.IsSuccessStatusCode)
        {
            _searchResults = await response.Content.ReadFromJsonAsync<List<Member>>() ?? new();
        }
        _isSearching = false;
    }

    private void SelectMember(Member member)
    {
        _selectedMember = member;
    }

    private async Task CheckIn()
    {
        if (_selectedMember == null) return;

        var response = await HttpClient.PostAsJsonAsync("/api/attendance/check-in", new { memberId = _selectedMember.MemberId });
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add($"Checked in {_selectedMember.FirstName}", Severity.Success);
            _selectedMember = null;
            _searchQuery = "";
            _searchResults.Clear();
            NavigationManager.NavigateTo("/RecordView");
        }
        else
        {
            Snackbar.Add("Check-in failed", Severity.Error);
        }
    }
}