using System.Threading.Tasks;
using CheckIn.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class ForgotPasswordDialog
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private string _email = "";
    private bool _isProcessing = false;

    private void Cancel() => MudDialog.Cancel();

    private async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_email)) return;
        
        _isProcessing = true;
        if (await AuthService.ForgotPasswordAsync(_email))
        {
            Snackbar.Add("If an account exists for that email, a temporary password has been sent.", Severity.Info);
            MudDialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add("Failed to process request. Please check SMTP settings.", Severity.Error);
        }
        _isProcessing = false;
    }
}
