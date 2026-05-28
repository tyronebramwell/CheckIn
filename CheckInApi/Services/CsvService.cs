using Microsoft.EntityFrameworkCore;
using CheckInApi.Data;
using System.Text;
using CheckInCommon.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CheckInApi.Services;

public class CsvService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _drivePath;
    private readonly LogService _log;

    public CsvService(IServiceScopeFactory scopeFactory, LogService log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
        _drivePath = Path.Combine(Directory.GetCurrentDirectory(), "drive");
        if (!Directory.Exists(_drivePath)) Directory.CreateDirectory(_drivePath);
    }

    public async Task ImportFromCsvAsync()
    {
        await ImportEventsAsync();
        await ImportMembersAsync();
        
        // Find all attendance files and import them
        var attendanceFiles = Directory.GetFiles(_drivePath, "attendance_*.csv");
        foreach (var filePath in attendanceFiles)
        {
            await ImportAttendanceFileAsync(filePath);
        }
    }

    private async Task ImportEventsAsync()
    {
        var filePath = Path.Combine(_drivePath, "events_latest.csv");
        if (!File.Exists(filePath)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null });
            
            var records = csv.GetRecords<dynamic>().ToList();
            int importedCount = 0;

            foreach (var record in records)
            {
                var dict = (IDictionary<string, object>)record;
                string name = dict.ContainsKey("Name") ? dict["Name"]?.ToString() ?? "" : "";
                if (string.IsNullOrEmpty(name)) continue;

                DateOnly eventDate = DateOnly.FromDateTime(DateTime.UtcNow);
                if (dict.ContainsKey("EventDate") && DateOnly.TryParse(dict["EventDate"].ToString(), out var parsedDate))
                {
                    eventDate = parsedDate;
                }

                var existing = await db.Events.AnyAsync(e => e.Name.ToLower() == name.ToLower() && e.EventDate == eventDate);
                if (!existing)
                {
                    var ev = new Event
                    {
                        Name = name,
                        EventDate = eventDate
                    };
                    
                    if (dict.ContainsKey("EventId") && Guid.TryParse(dict["EventId"].ToString(), out var eid))
                    {
                        ev.EventId = eid;
                    }

                    db.Events.Add(ev);
                    importedCount++;
                }
            }

            if (importedCount > 0)
            {
                await db.SaveChangesAsync();
                await _log.LogAsync($"Imported {importedCount} new events from CSV");
            }
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"Error importing events from CSV: {ex.Message}", true);
        }
    }

    private async Task ImportMembersAsync()
    {
        var filePath = Path.Combine(_drivePath, "members_latest.csv");
        if (!File.Exists(filePath)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null });
            
            var records = csv.GetRecords<dynamic>().ToList();
            int importedCount = 0;

            foreach (var record in records)
            {
                var dict = (IDictionary<string, object>)record;
                string username = dict.ContainsKey("Username") ? dict["Username"]?.ToString() ?? "" : "";
                string email = dict.ContainsKey("UserEmail") ? dict["UserEmail"]?.ToString() ?? "" : "";

                if (string.IsNullOrEmpty(username)) continue;

                var existing = await db.Members.AnyAsync(m => m.Username.ToLower() == username.ToLower());
                if (!existing)
                {
                    var member = new Member
                    {
                        FirstName = dict.ContainsKey("FirstName") ? dict["FirstName"]?.ToString() ?? "Imported" : "Imported",
                        LastName = dict.ContainsKey("LastName") ? dict["LastName"]?.ToString() ?? "User" : "User",
                        Username = username,
                        UserEmail = string.IsNullOrEmpty(email) ? $"{username}@imported.local" : email,
                        DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)), // Placeholder
                        Guardian1Name = dict.ContainsKey("Guardian1Name") ? dict["Guardian1Name"]?.ToString() ?? "N/A" : "N/A",
                        Guardian1Phone = CleanPhone(dict.ContainsKey("Guardian1Phone") ? dict["Guardian1Phone"]?.ToString() : null),
                        Guardian2Phone = CleanPhone(dict.ContainsKey("Guardian2Phone") ? dict["Guardian2Phone"]?.ToString() : null),
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("CheckIn123!"),
                        MustChangePassword = true,
                        AllowEmail = false,
                        QrSecret = Guid.NewGuid()
                    };
                    
                    if (dict.ContainsKey("MemberId") && Guid.TryParse(dict["MemberId"].ToString(), out var mid))
                    {
                        member.MemberId = mid;
                    }

                    db.Members.Add(member);
                    importedCount++;
                }
            }

            if (importedCount > 0)
            {
                await db.SaveChangesAsync();
                await _log.LogAsync($"Imported {importedCount} new members from CSV");
            }
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"Error importing members from CSV: {ex.Message}", true);
        }
    }

    private string CleanPhone(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "000";
        // Strip ="..." if it exists
        if (val.StartsWith("=\"") && val.EndsWith("\""))
        {
            return val.Substring(2, val.Length - 3);
        }
        return val;
    }

    private async Task ImportAttendanceFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null });
            
            var records = csv.GetRecords<dynamic>().ToList();
            int importedCount = 0;

            foreach (var record in records)
            {
                var dict = (IDictionary<string, object>)record;
                if (!dict.ContainsKey("Username")) continue;
                
                string username = dict["Username"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(username)) continue;

                var member = await db.Members.FirstOrDefaultAsync(m => m.Username.ToLower() == username.ToLower());
                if (member == null) continue;

                // Check for existing log around the same time to avoid duplicates
                if (dict.ContainsKey("CheckInTime") && DateTime.TryParse(dict["CheckInTime"].ToString(), out var checkIn))
                {
                    checkIn = DateTime.SpecifyKind(checkIn, DateTimeKind.Utc);
                    var existingLog = await db.AttendanceLogs.AnyAsync(l => l.MemberId == member.MemberId && l.CheckInTime == checkIn);
                    
                    if (!existingLog)
                    {
                        var log = new AttendanceLog
                        {
                            MemberId = member.MemberId,
                            CheckInTime = checkIn
                        };

                        if (dict.ContainsKey("CheckOutTime") && DateTime.TryParse(dict["CheckOutTime"].ToString(), out var checkOut))
                        {
                            log.CheckOutTime = DateTime.SpecifyKind(checkOut, DateTimeKind.Utc);
                        }
                        
                        // Try to find the event by name if provided
                        if (dict.ContainsKey("Event") || dict.ContainsKey("EventName"))
                        {
                            string eventName = (dict.ContainsKey("Event") ? dict["Event"] : dict["EventName"])?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(eventName) && eventName != "General")
                            {
                                var ev = await db.Events.FirstOrDefaultAsync(e => e.Name.ToLower() == eventName.ToLower() && e.EventDate == DateOnly.FromDateTime(checkIn.Date));
                                if (ev != null) log.EventId = ev.EventId;
                            }
                        }

                        db.AttendanceLogs.Add(log);
                        importedCount++;
                    }
                }
            }

            if (importedCount > 0)
            {
                await db.SaveChangesAsync();
                await _log.LogAsync($"Imported {importedCount} attendance records from {Path.GetFileName(filePath)}");
            }
        }
        catch (Exception ex)
        {
            await _log.LogAsync($"Error importing attendance from {Path.GetFileName(filePath)}: {ex.Message}", true);
        }
    }

    public async Task SyncEventsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var events = await db.Events.ToListAsync();
        var csv = new StringBuilder();
        csv.AppendLine("EventId,Name,EventDate");

        foreach (var e in events)
        {
            csv.AppendLine($"{e.EventId},{e.Name},{e.EventDate:yyyy-MM-dd}");
        }

        var filePath = Path.Combine(_drivePath, "events_latest.csv");
        await File.WriteAllTextAsync(filePath, csv.ToString());
    }

    public async Task SyncMembersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var members = await db.Members.ToListAsync();
        var csv = new StringBuilder();
        csv.AppendLine("MemberId,FirstName,LastName,DateOfBirth,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,UserEmail,Notes,Username");

        foreach (var m in members)
        {
            csv.AppendLine($"{m.MemberId},{m.FirstName},{m.LastName},{m.DateOfBirth:yyyy-MM-dd},\"{m.Guardian1Name}\",\"=\"\"{m.Guardian1Phone}\"\"\",\"\"{m.Guardian2Name}\"\",\"=\"\"{m.Guardian2Phone}\"\"\",{m.UserEmail},\"{m.Notes?.Replace("\"", "'")}\",{m.Username}");
        }

        var filePath = Path.Combine(_drivePath, "members_latest.csv");
        await File.WriteAllTextAsync(filePath, csv.ToString());
    }

    public async Task SyncAttendanceAsync(DateTime date)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var targetDate = date.Date;
        var nextDay = targetDate.AddDays(1);

        var logs = await db.AttendanceLogs
            .Include(l => l.Member)
            .Include(l => l.Event)
            .Where(l => l.CheckInTime >= targetDate && l.CheckInTime < nextDay)
            .OrderBy(l => l.CheckInTime)
            .ToListAsync();
        
        var csv = new StringBuilder();
        csv.AppendLine("LogId,MemberId,Username,FirstName,LastName,Event,CheckInTime,CheckOutTime,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,Notes");

        foreach (var l in logs)
        {
            var m = l.Member!;
            var evName = l.Event != null ? l.Event.Name : "General";
            csv.AppendLine($"{l.LogId},{l.MemberId},{m.Username},{m.FirstName},{m.LastName},{evName},{l.CheckInTime:yyyy-MM-dd HH:mm:ss},{l.CheckOutTime:yyyy-MM-dd HH:mm:ss},\"{m.Guardian1Name}\",\"=\"\"{m.Guardian1Phone}\"\"\",\"\"{m.Guardian2Name}\"\",\"=\"\"{m.Guardian2Phone}\"\"\",\"{m.Notes?.Replace("\"", "'")}\"");
        }

        var fileName = $"attendance_{targetDate:yyyyMMdd}.csv";
        var filePath = Path.Combine(_drivePath, fileName);
        await File.WriteAllTextAsync(filePath, csv.ToString());
    }
}
