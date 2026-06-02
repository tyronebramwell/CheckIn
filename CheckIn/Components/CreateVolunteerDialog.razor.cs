using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class CreateVolunteerDialog
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    private string _username = "";
    private string _email = "";
    private string _password = "";
    private bool _canViewData = true;
    private bool _canAddUsers = false;
    private bool _canManageEvents = false;
    private bool _canManageVolunteers = false;

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        var dto = new { 
            Username = _username, 
            Email = _email,
            Password = _password, 
            CanViewData = _canViewData, 
            CanAddUsers = _canAddUsers, 
            CanManageEvents = _canManageEvents,
            CanManageVolunteers = _canManageVolunteers 
        };
        
        var response = await HttpClient.PostAsJsonAsync("/api/volunteers", dto);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Volunteer created", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Snackbar.Add(error ?? "Failed to create volunteer", Severity.Error);
        }
    }
}
