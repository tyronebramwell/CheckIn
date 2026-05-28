using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using CheckInApi.Data;
using CheckInCommon.Models;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CheckInApi.Services;

public class EmailService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly LogService _log;

    public EmailService(IServiceScopeFactory scopeFactory, IConfiguration configuration, LogService log)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _log = log;
    }

    private async Task<List<SystemConfig>> GetConfigsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SystemConfigs.ToListAsync();
    }

    public async Task<bool> SendTemporaryPasswordEmailAsync(string toEmail, string username, string tempPassword)
    {
        string orgName = (await GetConfigsAsync()).FirstOrDefault(c => c.Key == "ORG_NAME")?.Value ?? _configuration["ORG_NAME"] ?? "Charity Check-In";
        string subject = $"Temporary Password for {orgName}";
        string body = $@"
            <div style='font-family: sans-serif; text-align: center; padding: 20px;'>
                <h2>Hello {username},</h2>
                <p>We received a request to reset your password.</p>
                <p>Your temporary password is: <strong style='font-size: 1.5rem; color: #1b6ec2;'>{tempPassword}</strong></p>
                <p>Please log in with this password. You will be required to change it immediately upon login.</p>
                <p style='color: #666; font-size: 0.8rem; margin-top: 30px;'>If you did not request this, please contact a volunteer.</p>
            </div>";

        return await SendEmailAsync(toEmail, username, subject, body);
    }

    private async Task<bool> SendEmailAsync(string toEmail, string username, string subject, string htmlBody, byte[]? qrPngBytes = null)
    {
        var configs = await GetConfigsAsync();
        string orgName = configs.FirstOrDefault(c => c.Key == "ORG_NAME")?.Value ?? _configuration["ORG_NAME"] ?? "Charity Check-In";
        string host = configs.FirstOrDefault(c => c.Key == "SMTP_HOST")?.Value ?? _configuration["SMTP_HOST"] ?? "smtp.gmail.com";
        int port = int.Parse(configs.FirstOrDefault(c => c.Key == "SMTP_PORT")?.Value ?? _configuration["SMTP_PORT"] ?? "587");
        string user = configs.FirstOrDefault(c => c.Key == "SMTP_USER")?.Value ?? _configuration["SMTP_USER"] ?? "";
        string pass = configs.FirstOrDefault(c => c.Key == "SMTP_PASS")?.Value ?? _configuration["SMTP_PASS"] ?? "";

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            await _log.LogAsync($"Email failed to {toEmail}: SMTP credentials not configured", true);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(orgName, user));
            message.To.Add(new MailboxAddress(username, toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };

            if (qrPngBytes != null)
            {
                var image = builder.LinkedResources.Add("qr_code.png", qrPngBytes);
                image.ContentId = MimeUtils.GenerateMessageId();
                // Replace placeholder in body
                builder.HtmlBody = builder.HtmlBody.Replace("{QR_CID}", image.ContentId);
                
                // Also add as a regular attachment
                builder.Attachments.Add("login_qr_code.png", qrPngBytes);
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            await _log.LogAsync($"Email '{subject}' sent successfully to {toEmail}");
            return true;
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"Email error to {toEmail}: {ex.Message}", true);
            return false;
        }
    }

    public async Task<bool> SendQrCodeEmailAsync(string toEmail, string username, string qrText)
    {
        var configs = await GetConfigsAsync();
        string orgName = configs.FirstOrDefault(c => c.Key == "ORG_NAME")?.Value ?? _configuration["ORG_NAME"] ?? "Charity Check-In";
        
        string subject = $"{orgName} - Your Login QR Code";
        
        // Generate PNG using QRCoder
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrPngBytes = qrCode.GetGraphic(20); // 20 is pixels per module

        string body = $@"
            <div style='font-family: sans-serif; text-align: center; padding: 20px;'>
                <h2>Hello {username},</h2>
                <p>Attached is your login QR code for the {orgName} terminal.</p>
                <div style='background: white; padding: 20px; display: inline-block; border-radius: 10px; border: 1px solid #ddd;'>
                    <img src='cid:{{QR_CID}}' alt='Login QR Code' style='width: 250px; height: 250px;' />
                </div>
                <p style='margin-top: 20px;'>Simply show this code to the camera to check in instantly.</p>
                <p style='color: #666; font-size: 0.8rem;'>A copy of this code is also attached to this email as a PNG file.</p>
            </div>";

        return await SendEmailAsync(toEmail, username, subject, body, qrPngBytes);
    }
}
