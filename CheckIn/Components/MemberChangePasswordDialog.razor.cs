using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class MemberChangePasswordDialog
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private string _newPassword = "";

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_newPassword))
        {
            Snackbar.Add("Password cannot be empty.", Severity.Warning);
            return;
        }

        var dto = new { NewPassword = _newPassword };
        var response = await HttpClient.PutAsJsonAsync("/api/members/self/password", dto);
        
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("Password updated successfully. Please log in again.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add("Failed to update password.", Severity.Error);
        }
    }
}
