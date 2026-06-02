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

public partial class Events
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<Event> _events = new();
    private bool _showCreateDialog = false;
    
    private string _eventName = "";
    private DateTime? _eventDate = DateTime.Today;
    private string _repeatType = "None";
    private int _repeatCount = 1;

    private DialogOptions _dialogOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    protected override async Task OnInitializedAsync()
    {
        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        var response = await HttpClient.GetAsync("/api/events");
        if (response.IsSuccessStatusCode)
        {
            _events = await response.Content.ReadFromJsonAsync<List<Event>>() ?? new();
        }
    }

    private void OpenCreateDialog()
    {
        _eventName = "";
        _eventDate = DateTime.Today;
        _repeatType = "None";
        _repeatCount = 1;
        _showCreateDialog = true;
    }

    private async Task SaveEvent()
    {
        if (string.IsNullOrWhiteSpace(_eventName) || _eventDate == null) return;
        
        var dto = new CreateEventDto(
            _eventName, 
            DateOnly.FromDateTime(_eventDate.Value), 
            _repeatType, 
            _repeatCount
        );

        var response = await HttpClient.PostAsJsonAsync("/api/events", dto);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Event(s) created successfully", Severity.Success);
            _showCreateDialog = false;
            await LoadEvents();
        }
    }

    private async Task DeleteEvent(Event ev)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync("Delete Event", $"Are you sure you want to delete '{ev.Name}'?", "Delete", "Cancel");
        if (confirmed == true)

        {
            var response = await HttpClient.DeleteAsync($"/api/events/{ev.EventId}");
            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("Event deleted", Severity.Info);
                await LoadEvents();
            }
        }
    }

    public record CreateEventDto(string Name, DateOnly EventDate, string RepeatType, int RepeatCount);
}