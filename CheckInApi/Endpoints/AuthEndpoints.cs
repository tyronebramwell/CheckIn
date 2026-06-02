using System.Security.Claims;
using CheckInApi.Data;
using CheckInApi.Models;
using CheckInApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (ClaimsPrincipal user, LogService log) =>
        {
            var userType = user.FindFirstValue("UserType");
            var canViewData = user.FindFirstValue("CanViewData") == "true";
            var canAddUsers = user.FindFirstValue("CanAddUsers") == "true";
            var canManageVolunteers = user.FindFirstValue("CanManageVolunteers") == "true";
            var mustChangePassword = user.FindFirstValue("MustChangePassword") == "true";

            await log.LogAsync($"Login successful as {userType}");

            return Results.Ok(new { userType, canViewData, canAddUsers, canManageVolunteers, mustChangePassword });
        }).RequireAuthorization();

        group.MapPost("/forgot-password", async (AppDbContext db, EmailService emailService, LogService log, ForgotPasswordRequest request) =>
        {
            var member = await db.Members.SingleOrDefaultAsync(m => m.UserEmail.ToLower() == request.Email.ToLower());
            if (member == null)
            {
                await log.LogAsync($"Forgot password attempt for unknown email: {request.Email}", true);
                return Results.Ok(); 
            }

            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            var tempPassword = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());

            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            member.MustChangePassword = true;
            await db.SaveChangesAsync();

            _ = emailService.SendTemporaryPasswordEmailAsync(member.UserEmail, member.Username, tempPassword);
            
            await log.LogAsync($"Temporary password sent to {member.Username}");
            return Results.Ok();
        }).AllowAnonymous();

        group.MapPost("/change-password-reset", async (AppDbContext db, LogService log, ClaimsPrincipal user, ChangePasswordResetRequest request) =>
        {
            var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
            
            var member = await db.Members.FindAsync(Guid.Parse(memberIdStr));
            if (member == null) return Results.NotFound();

            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            member.MustChangePassword = false;
            await db.SaveChangesAsync();

            await log.LogAsync($"Password reset completed for {member.Username}");
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/qr-login", async (AppDbContext db, LogService log, QrLoginRequest request) =>
        {
            var member = await db.Members.SingleOrDefaultAsync(m => m.Username.ToLower() == request.Username.ToLower() && m.QrSecret == request.QrSecret);
            
            if (member == null)
            {
                await log.LogAsync($"Failed QR login attempt for username: {request.Username}", true);
                return Results.Unauthorized();
            }

            await log.LogAsync($"QR login successful for member: {member.Username}");
            
            return Results.Ok(new { 
                userType = "Member", 
                username = member.Username,
                memberId = member.MemberId
            });
        }).AllowAnonymous();
    }
}
