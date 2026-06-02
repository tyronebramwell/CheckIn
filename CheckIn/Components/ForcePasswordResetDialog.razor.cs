using System.Threading.Tasks;
using CheckIn.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class ForcePasswordResetDialog
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private string _newPassword = "";
    private string _confirmPassword = "";
    private bool _isProcessing = false;

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_newPassword)) return;
        if (_newPassword != _confirmPassword)
        {
            Snackbar.Add("Passwords do not match.", Severity.Warning);
            return;
        }

        _isProcessing = true;
        if (await AuthService.ChangePasswordResetAsync(_newPassword))
        {
            Snackbar.Add("Password updated successfully.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add("Failed to update password.", Severity.Error);
        }
        _isProcessing = false;
    }
}
