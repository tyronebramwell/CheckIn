using System.Security.Claims;
using CheckInApi.Data;
using CheckInApi.Models;
using CheckInApi.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Endpoints;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance");

        group.MapPost("/check-in", async (AppDbContext db, CsvService csv, LogService log, CheckInRequest request) =>
        {
            var member = await db.Members.FindAsync(request.MemberId);
            var logMsg = member != null ? $"Volunteer checked in member: {member.Username}" : $"Volunteer checked in unknown member: {request.MemberId}";
            
            var attendanceLog = new AttendanceLog
            {
                MemberId = request.MemberId,
                CheckInTime = DateTime.UtcNow
            };
            db.AttendanceLogs.Add(attendanceLog);
            await db.SaveChangesAsync();
            
            await log.LogAsync(logMsg);
            
            return Results.Ok(attendanceLog);
        }).RequireAuthorization("CanViewData");

        group.MapPut("/check-out", async (AppDbContext db, CsvService csv, LogService log, CheckOutRequest request) =>
        {
            var attendanceLog = await db.AttendanceLogs.Include(l => l.Member).FirstOrDefaultAsync(l => l.LogId == request.LogId);
            if (attendanceLog == null) return Results.NotFound();
            
            attendanceLog.CheckOutTime = DateTime.UtcNow;
            await db.SaveChangesAsync();
            
            await log.LogAsync($"Volunteer checked out member: {attendanceLog.Member?.Username}");

            return Results.Ok(attendanceLog);
        }).RequireAuthorization("CanViewData");

        group.MapPost("/self-check-in", async (AppDbContext db, CsvService csv, LogService log, ClaimsPrincipal user, [FromQuery] Guid? eventId) =>
        {
            var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
            var memberId = Guid.Parse(memberIdStr);

            var activeLog = await db.AttendanceLogs.Include(l => l.Event).FirstOrDefaultAsync(l => l.MemberId == memberId && l.CheckOutTime == null);
            if (activeLog != null)
            {
                activeLog.CheckOutTime = DateTime.UtcNow;
                await log.LogAsync($"Auto Check-Out from {activeLog.Event?.Name ?? "previous event"} during new check-in");
            }

            if (eventId == null)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var todayEvents = await db.Events.Where(e => e.EventDate == today).ToListAsync();
                if (todayEvents.Count > 1)
                {
                    return Results.Conflict(new { message = "Multiple events active", events = todayEvents });
                }
                eventId = todayEvents.FirstOrDefault()?.EventId;
            }

            var attendanceLog = new AttendanceLog
            {
                MemberId = memberId,
                EventId = eventId,
                CheckInTime = DateTime.UtcNow
            };
            db.AttendanceLogs.Add(attendanceLog);
            await db.SaveChangesAsync();
            
            await log.LogAsync("Member self-checked in");

            return Results.Ok(attendanceLog);
        }).RequireAuthorization("IsMember");

        group.MapPost("/self-check-out", async (AppDbContext db, CsvService csv, LogService log, ClaimsPrincipal user) =>
        {
            var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
            var memberId = Guid.Parse(memberIdStr);

            var attendanceLog = await db.AttendanceLogs
                .Where(l => l.MemberId == memberId && l.CheckOutTime == null)
                .OrderByDescending(l => l.CheckInTime)
                .FirstOrDefaultAsync();

            if (attendanceLog == null) 
            {
                await log.LogAsync("Failed self-check-out: No active check-in found", true);
                return Results.NotFound("No active check-in found.");
            }
            
            attendanceLog.CheckOutTime = DateTime.UtcNow;
            await db.SaveChangesAsync();
            
            await log.LogAsync("Member self-checked out");

            return Results.Ok(attendanceLog);
        }).RequireAuthorization("IsMember");

        group.MapPost("/qr-action", async (AppDbContext db, CsvService csv, LogService log, QrLoginRequest request, [FromQuery] Guid? eventId) =>
        {
            var member = await db.Members.SingleOrDefaultAsync(m => m.Username.ToLower() == request.Username.ToLower() && m.QrSecret == request.QrSecret);
            
            if (member == null)
            {
                await log.LogAsync($"Invalid QR action attempt for username: {request.Username}", true);
                return Results.Unauthorized();
            }

            var activeLog = await db.AttendanceLogs
                .Include(l => l.Event)
                .Where(l => l.MemberId == member.MemberId && l.CheckOutTime == null)
                .OrderByDescending(l => l.CheckInTime)
                .FirstOrDefaultAsync();

            string action;
            if (activeLog == null)
            {
                if (eventId == null)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var todayEvents = await db.Events.Where(e => e.EventDate == today).ToListAsync();
                    if (todayEvents.Count > 1)
                    {
                        return Results.Conflict(new { action = "RequiresSelection", events = todayEvents });
                    }
                    eventId = todayEvents.FirstOrDefault()?.EventId;
                }

                var logEntry = new AttendanceLog
                {
                    MemberId = member.MemberId,
                    EventId = eventId,
                    CheckInTime = DateTime.UtcNow
                };
                db.AttendanceLogs.Add(logEntry);
                action = "CheckedIn";
                await log.LogAsync($"QR Direct Check-In: {member.Username}");
            }
            else
            {
                activeLog.CheckOutTime = DateTime.UtcNow;
                action = "CheckedOut";
                await log.LogAsync($"QR Direct Check-Out: {member.Username} (from {activeLog.Event?.Name ?? "event"})");
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { action, username = member.Username, firstName = member.FirstName, lastName = member.LastName });
        }).AllowAnonymous();

        group.MapGet("/self-status", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
            var memberId = Guid.Parse(memberIdStr);

            var isActive = await db.AttendanceLogs
                .AnyAsync(l => l.MemberId == memberId && l.CheckOutTime == null);

            return Results.Ok(new { isActive });
        }).RequireAuthorization("IsMember");

        group.MapGet("/active", async (AppDbContext db) =>
        {
            var activeAttendance = await db.AttendanceLogs
                .Include(l => l.Member)
                .Include(l => l.Event)
                .Where(l => l.CheckOutTime == null)
                .Select(l => new
                {
                    l.LogId,
                    l.MemberId,
                    l.Member!.FirstName,
                    l.Member!.LastName,
                    l.Member!.Notes,
                    l.Member!.Guardian1Phone,
                    l.Member!.Guardian2Phone,
                    l.CheckInTime,
                    EventName = l.Event != null ? l.Event.Name : "General"
                })
                .ToListAsync();
            return Results.Ok(activeAttendance);
        }).RequireAuthorization("CanViewData");

        group.MapGet("/history", async (AppDbContext db, DateTime? date) =>
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.Date;
            var nextDay = targetDate.AddDays(1);

            var history = await db.AttendanceLogs
                .Include(l => l.Member)
                .Include(l => l.Event)
                .Where(l => l.CheckInTime >= targetDate && l.CheckInTime < nextDay)
                .OrderByDescending(l => l.CheckInTime)
                .Select(l => new
                {
                    l.LogId,
                    l.MemberId,
                    l.Member!.FirstName,
                    l.Member!.LastName,
                    l.CheckInTime,
                    l.CheckOutTime,
                    l.Member!.Guardian1Phone,
                    EventName = l.Event != null ? l.Event.Name : "General"
                })
                .ToListAsync();
            return Results.Ok(history);
        }).RequireAuthorization("CanViewData");

        group.MapGet("/export", async (AppDbContext db, DateTime? date) =>
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.Date;
            var nextDay = targetDate.AddDays(1);

            var logs = await db.AttendanceLogs
                .Include(l => l.Member)
                .Include(l => l.Event)
                .Where(l => l.CheckInTime >= targetDate && l.CheckInTime < nextDay)
                .OrderBy(l => l.CheckInTime)
                .ToListAsync();
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("LogId,MemberId,Username,FirstName,LastName,Event,CheckInTime,CheckOutTime,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,Notes");

            foreach (var l in logs)
            {
                var m = l.Member!;
                var evName = l.Event != null ? l.Event.Name : "General";
                csv.AppendLine($"{l.LogId},{l.MemberId},{m.Username},{m.FirstName},{m.LastName},{evName},{l.CheckInTime:yyyy-MM-dd HH:mm:ss},{l.CheckOutTime:yyyy-MM-dd HH:mm:ss},\"{m.Guardian1Name}\",\"=\"\"{m.Guardian1Phone}\"\"\",\"\"{m.Guardian2Name}\"\",\"=\"\"{m.Guardian2Phone}\"\"\",\"{m.Notes?.Replace("\"", "'")}\"");
            }

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "drive");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            
            var fileName = $"attendance_export_{targetDate:yyyyMMdd}_{DateTime.UtcNow:HHmmss}.csv";
            var filePath = Path.Combine(directory, fileName);
            
            await File.WriteAllTextAsync(filePath, csv.ToString());
            
            return Results.Ok(new { fileName, filePath = "/app/drive/" + fileName, recordCount = logs.Count });
        }).RequireAuthorization("CanManageVolunteers");
    }
}
