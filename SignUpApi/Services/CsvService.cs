using Microsoft.EntityFrameworkCore;
using SignUpApi.Data;
using System.Text;

namespace SignUpApi.Services;

public class CsvService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _drivePath;

    public CsvService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _drivePath = Path.Combine(Directory.GetCurrentDirectory(), "drive");
        if (!Directory.Exists(_drivePath)) Directory.CreateDirectory(_drivePath);
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
            csv.AppendLine($"{m.MemberId},{m.FirstName},{m.LastName},{m.DateOfBirth:yyyy-MM-dd},{m.Guardian1Name},{m.Guardian1Phone},{m.Guardian2Name},{m.Guardian2Phone},{m.UserEmail},\"{m.Notes?.Replace("\"", "'")}\",{m.Username}");
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
            .Where(l => l.CheckInTime >= targetDate && l.CheckInTime < nextDay)
            .OrderBy(l => l.CheckInTime)
            .ToListAsync();
        
        var csv = new StringBuilder();
        csv.AppendLine("LogId,MemberId,Username,FirstName,LastName,CheckInTime,CheckOutTime,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,Notes");

        foreach (var l in logs)
        {
            var m = l.Member!;
            csv.AppendLine($"{l.LogId},{l.MemberId},{m.Username},{m.FirstName},{m.LastName},{l.CheckInTime:yyyy-MM-dd HH:mm:ss},{l.CheckOutTime:yyyy-MM-dd HH:mm:ss},\"{m.Guardian1Name}\",\"{m.Guardian1Phone}\",\"{m.Guardian2Name}\",\"{m.Guardian2Phone}\",\"{m.Notes?.Replace("\"", "'")}\"");
        }

        var fileName = $"attendance_{targetDate:yyyyMMdd}.csv";
        var filePath = Path.Combine(_drivePath, fileName);
        await File.WriteAllTextAsync(filePath, csv.ToString());
    }
}
