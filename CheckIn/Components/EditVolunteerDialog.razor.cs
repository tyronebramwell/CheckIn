using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class EditVolunteerDialog
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public CheckIn.Pages.VolunteerSummary Volunteer { get; set; } = null!;

    private string? _email;
    private bool _canViewData;
    private bool _canAddUsers;
    private bool _canManageEvents;
    private bool _canManageVolunteers;

    protected override void OnInitialized()
    {
        _email = Volunteer.Email;
        _canViewData = Volunteer.CanViewData;
        _canAddUsers = Volunteer.CanAddUsers;
        _canManageEvents = Volunteer.CanManageEvents;
        _canManageVolunteers = Volunteer.CanManageVolunteers;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        var dto = new { 
            Email = _email,
            CanViewData = _canViewData, 
            CanAddUsers = _canAddUsers, 
            CanManageEvents = _canManageEvents,
            CanManageVolunteers = _canManageVolunteers 
        };
        
        var response = await HttpClient.PutAsJsonAsync($"/api/volunteers/{Volunteer.VolunteerId}/permissions", dto);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Volunteer updated", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add("Failed to update volunteer", Severity.Error);
        }
    }
}
