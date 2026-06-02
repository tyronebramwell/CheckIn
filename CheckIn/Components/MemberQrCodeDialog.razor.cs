using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Net.Codecrete.QrCodeGenerator;

namespace CheckIn.Components;

public partial class MemberQrCodeDialog
{
    [Inject] private HttpClient HttpClient { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public Guid MemberId { get; set; }
    [Parameter] public string Username { get; set; } = "";
    [Parameter] public Guid QrSecret { get; set; }

    private string _qrSvg = "";
    private bool _isRegenerating = false;
    private bool _isSendingEmail = false;

    protected override void OnInitialized()
    {
        GenerateQrCode();
    }

    private string GetTruncatedSecret()
    {
        var secretString = QrSecret.ToString();
        return secretString.Length > 8 ? $"{secretString.Substring(0, 8)}..." : secretString;
    }

    private void GenerateQrCode()
    {
        try
        {
            var qrText = $"{Username}|{QrSecret}";
            var qr = QrCode.EncodeText(qrText, QrCode.Ecc.Medium);
            _qrSvg = qr.ToSvgString(4);
        }
        catch (Exception ex)
        {
            _qrSvg = $"<text>Error generating QR: {ex.Message}</text>";
        }
    }

    private async Task RegenerateQr()
    {
        _isRegenerating = true;
        var response = await HttpClient.PostAsync($"/api/members/{MemberId}/regenerate-qr", null);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RegenerateResponse>();
            if (result != null)
            {
                QrSecret = result.QrSecret;
                GenerateQrCode();
                Snackbar.Add("QR Code regenerated. The old one is now blocked.", Severity.Warning);
            }
        }
        else
        {
            Snackbar.Add("Failed to regenerate QR code.", Severity.Error);
        }
        _isRegenerating = false;
    }

    private async Task SendEmail()
    {
        _isSendingEmail = true;
        var response = await HttpClient.PostAsync($"/api/members/{MemberId}/send-qr-email", null);
        if (response.IsSuccessStatusCode)
        {
            Snackbar.Add("QR Code sent successfully via email.", Severity.Success);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Snackbar.Add(error ?? "Failed to send email.", Severity.Error);
        }
        _isSendingEmail = false;
    }

    private void Close() => MudDialog.Close();
}

public record RegenerateResponse(Guid QrSecret);
