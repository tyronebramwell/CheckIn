using System.Security.Claims;
using CheckInApi.Data;
using CheckInApi.Models;
using CheckInApi.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CheckInApi.Endpoints;

public static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members");

        group.MapPost("/public", async (AppDbContext db, CsvService csv, LogService log, EmailService emailService, IConfiguration config, MemberRegistrationDto dto) =>
        {
            var allowPublic = config.GetValue<bool>("ALLOW_PUBLIC_REGISTRATION");
            if (!allowPublic) return Results.Forbid();

            if (await db.Members.AnyAsync(m => m.Username.ToLower() == dto.Username.ToLower()))
            {
                await log.LogAsync($"Failed public registration: Username {dto.Username} already exists", true);
                return Results.BadRequest("Username already exists.");
            }

            var member = new Member
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                Guardian1Name = dto.Guardian1Name,
                Guardian1Phone = dto.Guardian1Phone,
                Guardian2Name = dto.Guardian2Name,
                Guardian2Phone = dto.Guardian2Phone,
                UserEmail = dto.UserEmail,
                Notes = dto.Notes,
                AllowEmail = dto.AllowEmail,
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                QrSecret = Guid.NewGuid()
            };

            var preference = await db.ContactPreferences.FindAsync(dto.UserEmail);
            if (preference == null)
            {
                preference = new ContactPreference
                {
                    UserEmail = dto.UserEmail,
                    AcceptsNewsletter = dto.AcceptsNewsletter,
                    AcceptsSurveys = dto.AcceptsSurveys,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ContactPreferences.Add(preference);
            }
            else
            {
                preference.AcceptsNewsletter = dto.AcceptsNewsletter;
                preference.AcceptsSurveys = dto.AcceptsSurveys;
                preference.UpdatedAt = DateTime.UtcNow;
            }

            db.Members.Add(member);
            await db.SaveChangesAsync();
            
            if (member.AllowEmail)
            {
                var qrText = $"{member.Username}|{member.QrSecret}";
                _ = emailService.SendQrCodeEmailAsync(member.UserEmail, member.Username, qrText);
            }
            
            await log.LogAsync($"Public registration successful: {member.Username} ({member.MemberId})");
            
            return Results.Created($"/api/members/{member.MemberId}", member);
        }).AllowAnonymous();

        group.MapPost("/", async (AppDbContext db, CsvService csv, LogService log, EmailService emailService, MemberRegistrationDto dto) =>
        {
            if (await db.Members.AnyAsync(m => m.Username.ToLower() == dto.Username.ToLower()))
            {
                await log.LogAsync($"Failed registration attempt: Username {dto.Username} already exists", true);
                return Results.BadRequest("Username already exists.");
            }

            var member = new Member
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
                Guardian1Name = dto.Guardian1Name,
                Guardian1Phone = dto.Guardian1Phone,
                Guardian2Name = dto.Guardian2Name,
                Guardian2Phone = dto.Guardian2Phone,
                UserEmail = dto.UserEmail,
                Notes = dto.Notes,
                AllowEmail = dto.AllowEmail,
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                QrSecret = Guid.NewGuid()
            };

            var preference = await db.ContactPreferences.FindAsync(dto.UserEmail);
            if (preference == null)
            {
                preference = new ContactPreference
                {
                    UserEmail = dto.UserEmail,
                    AcceptsNewsletter = dto.AcceptsNewsletter,
                    AcceptsSurveys = dto.AcceptsSurveys,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ContactPreferences.Add(preference);
            }
            else
            {
                preference.AcceptsNewsletter = dto.AcceptsNewsletter;
                preference.AcceptsSurveys = dto.AcceptsSurveys;
                preference.UpdatedAt = DateTime.UtcNow;
            }

            db.Members.Add(member);
            await db.SaveChangesAsync();
            
            if (member.AllowEmail)
            {
                var qrText = $"{member.Username}|{member.QrSecret}";
                _ = emailService.SendQrCodeEmailAsync(member.UserEmail, member.Username, qrText);
            }
            
            await log.LogAsync($"Registered new member: {member.Username} ({member.MemberId})");

            return Results.Created($"/api/members/{member.MemberId}", member);
        }).RequireAuthorization("CanAddUsers");

        group.MapGet("/", async (AppDbContext db, string? search) =>
        {
            var query = db.Members.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => EF.Functions.ILike(c.FirstName, $"%{search}%") || EF.Functions.ILike(c.LastName, $"%{search}%"));
            }
            return Results.Ok(await query.ToListAsync());
        }).RequireAuthorization("CanViewData");

        group.MapGet("/{id}", async (AppDbContext db, LogService log, Guid id) =>
        {
            var member = await db.Members.FindAsync(id);
            if (member == null) return Results.NotFound();

            await log.LogAsync($"Viewed member profile: {member.Username} ({member.MemberId})");

            var preference = await db.ContactPreferences.FindAsync(member.UserEmail);
            
            var result = new
            {
                member.MemberId,
                member.FirstName,
                member.LastName,
                member.DateOfBirth,
                member.Guardian1Name,
                member.Guardian1Phone,
                member.Guardian2Name,
                member.Guardian2Phone,
                member.UserEmail,
                member.Notes,
                member.AllowEmail,
                member.Username,
                member.QrSecret,
                AcceptsNewsletter = preference?.AcceptsNewsletter ?? false,
                AcceptsSurveys = preference?.AcceptsSurveys ?? false
            };

            return Results.Ok(result);
        }).RequireAuthorization("CanViewData");

        group.MapGet("/export", async (AppDbContext db) =>
        {
            var members = await db.Members.ToListAsync();
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("MemberId,Username,FirstName,LastName,DateOfBirth,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,UserEmail,Notes");

            foreach (var m in members)
            {
                csv.AppendLine($"{m.MemberId},{m.Username},{m.FirstName},{m.LastName},{m.DateOfBirth:yyyy-MM-dd},\"{m.Guardian1Name}\",\"=\"\"{m.Guardian1Phone}\"\"\",\"\"{m.Guardian2Name}\"\",\"=\"\"{m.Guardian2Phone}\"\"\",{m.UserEmail},\"{m.Notes?.Replace("\"", "'")}\"");
            }

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "drive");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            
            var fileName = $"members_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(directory, fileName);
            
            await File.WriteAllTextAsync(filePath, csv.ToString());
            
            return Results.Ok(new { fileName, filePath = "/app/drive/" + fileName });
        }).RequireAuthorization("CanManageVolunteers");

        group.MapPut("/{id}", async (AppDbContext db, CsvService csv, LogService log, Guid id, MemberRegistrationDto dto) =>
        {
            var member = await db.Members.FindAsync(id);
            if (member == null) return Results.NotFound();

            if (await db.Members.AnyAsync(m => m.MemberId != id && m.Username.ToLower() == dto.Username.ToLower()))
            {
                await log.LogAsync($"Failed profile update for {member.Username}: Username {dto.Username} already exists", true);
                return Results.BadRequest("Username already exists.");
            }

            if (await db.Members.AnyAsync(m => m.MemberId != id && m.UserEmail.ToLower() == dto.UserEmail.ToLower()))
            {
                await log.LogAsync($"Failed profile update for {member.Username}: Email {dto.UserEmail} already exists", true);
                return Results.BadRequest("Email already exists.");
            }

            member.FirstName = dto.FirstName;
            member.LastName = dto.LastName;
            member.DateOfBirth = dto.DateOfBirth;
            member.Guardian1Name = dto.Guardian1Name;
            member.Guardian1Phone = dto.Guardian1Phone;
            member.Guardian2Name = dto.Guardian2Name;
            member.Guardian2Phone = dto.Guardian2Phone;
            member.UserEmail = dto.UserEmail;
            member.Notes = dto.Notes;
            member.AllowEmail = dto.AllowEmail;
            member.Username = dto.Username;
            
            if (!string.IsNullOrEmpty(dto.Password) && dto.Password != "********")
            {
                member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            var preference = await db.ContactPreferences.FindAsync(dto.UserEmail);
            if (preference == null)
            {
                preference = new ContactPreference
                {
                    UserEmail = dto.UserEmail,
                    AcceptsNewsletter = dto.AcceptsNewsletter,
                    AcceptsSurveys = dto.AcceptsSurveys,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ContactPreferences.Add(preference);
            }
            else
            {
                preference.AcceptsNewsletter = dto.AcceptsNewsletter;
                preference.AcceptsSurveys = dto.AcceptsSurveys;
                preference.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            
            await log.LogAsync($"Updated member profile: {member.Username} ({member.MemberId})");

            return Results.NoContent();
        }).RequireAuthorization("CanAddUsers");

        group.MapPost("/{id}/send-qr-email", async (AppDbContext db, EmailService emailService, Guid id) =>
        {
            var member = await db.Members.FindAsync(id);
            if (member == null) return Results.NotFound();

            var qrText = $"{member.Username}|{member.QrSecret}";
            var success = await emailService.SendQrCodeEmailAsync(member.UserEmail, member.Username, qrText);
            return success ? Results.Ok() : Results.BadRequest("Failed to send email. Check SMTP settings.");
        }).RequireAuthorization("CanAddUsers");

        group.MapPost("/{id}/regenerate-qr", async (AppDbContext db, LogService log, Guid id) =>
        {
            var member = await db.Members.FindAsync(id);
            if (member == null) return Results.NotFound();

            member.QrSecret = Guid.NewGuid();
            await db.SaveChangesAsync();

            await log.LogAsync($"Regenerated QR Secret for member: {member.Username} ({member.MemberId})");

            return Results.Ok(new { qrSecret = member.QrSecret });
        }).RequireAuthorization("CanAddUsers");

        group.MapPut("/self/password", async (AppDbContext db, LogService log, UpdatePasswordDto dto, ClaimsPrincipal user) =>
        {
            var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
            var memberId = Guid.Parse(memberIdStr);

            var member = await db.Members.FindAsync(memberId);
            if (member == null) return Results.NotFound();

            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await db.SaveChangesAsync();
            
            await log.LogAsync($"Member updated their own password: {member.Username}");

            return Results.NoContent();
        }).RequireAuthorization("IsMember");
    }
}
