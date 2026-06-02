using CheckInApi.Data;
using CheckInApi.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CheckInApi.Endpoints;

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/config", async (AppDbContext db, IConfiguration config) =>
        {
            var allowPublicEntry = await db.SystemConfigs.FindAsync("ALLOW_PUBLIC_REGISTRATION");
            bool allowPublic;

            if (allowPublicEntry != null)
            {
                allowPublic = bool.Parse(allowPublicEntry.Value);
            }
            else
            {
                allowPublic = config.GetValue<bool>("ALLOW_PUBLIC_REGISTRATION");
            }

            return Results.Ok(new { allowPublic });
        }).AllowAnonymous();

        app.MapPost("/api/config/sync-now", async (CsvService csv, LogService log) =>
        {
            await csv.ImportFromCsvAsync();
            await csv.SyncEventsAsync();
            await csv.SyncMembersAsync();
            await csv.SyncAttendanceAsync(DateTime.UtcNow);
            await log.LogAsync("Manual two-way CSV synchronization triggered by administrator");
            return Results.Ok();
        }).RequireAuthorization("CanManageVolunteers");

        app.MapGet("/api/config/admin", async (AppDbContext db, IConfiguration config) =>
        {
            var settings = await db.SystemConfigs.ToListAsync();
            
            var keys = new[] { "ALLOW_PUBLIC_REGISTRATION", "ORG_NAME", "SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASS", "CSV_SYNC_INTERVAL_MINS" };
            foreach (var key in keys)
            {
                if (!settings.Any(s => s.Key == key))
                {
                    settings.Add(new SystemConfig 
                    { 
                        Key = key, 
                        Value = config.GetValue<string>(key) ?? (key == "CSV_SYNC_INTERVAL_MINS" ? "5" : (key == "SMTP_PORT" ? "587" : (key == "SMTP_HOST" ? "smtp.gmail.com" : (key == "ORG_NAME" ? "Charity Check-In" : ""))))
                    });
                }
            }

            return Results.Ok(settings);
        }).RequireAuthorization("CanManageVolunteers");

        app.MapPut("/api/config", async (AppDbContext db, LogService log, List<SystemConfig> settings) =>
        {
            foreach (var setting in settings)
            {
                var existing = await db.SystemConfigs.FindAsync(setting.Key);
                if (existing != null)
                {
                    existing.Value = setting.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.SystemConfigs.Add(new SystemConfig
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();
            await log.LogAsync("System configuration updated by administrator");
            return Results.NoContent();
        }).RequireAuthorization("CanManageVolunteers");

        app.MapGet("/health", () => Results.Ok("API is running")).AllowAnonymous();
    }
}
