using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class UpdatePasswordDialog
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public Guid VolunteerId { get; set; }

    private string _newPassword = "";

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_newPassword)) return;

        var dto = new { NewPassword = _newPassword };
        var response = await HttpClient.PutAsJsonAsync($"/api/volunteers/{VolunteerId}/password", dto);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Password updated", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add("Failed to update password", Severity.Error);
        }
    }
}
