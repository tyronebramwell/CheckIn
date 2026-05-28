using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CheckInApi.Auth;
using CheckInApi.Data;
using CheckInApi.Services;
using CheckInCommon.Models;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Set global culture to UK
var culture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Fix for PostgreSQL DateTime issues
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CsvService>();
builder.Services.AddScoped<LogService>();

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewData", policy => policy.RequireClaim("CanViewData", "true"));
    options.AddPolicy("CanAddUsers", policy => policy.RequireClaim("CanAddUsers", "true"));
    options.AddPolicy("CanManageVolunteers", policy => policy.RequireClaim("CanManageVolunteers", "true"));
    options.AddPolicy("IsMember", policy => policy.RequireClaim("UserType", "Member"));
});

builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 5001;
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Charity Event API", Version = "v1" });
    c.AddSecurityDefinition("basic", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "basic",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Basic Authorization header using the Bearer scheme."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseHttpsRedirection();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Charity Event API V1");
    c.RoutePrefix = string.Empty;
});

app.MapGet("/health", () => Results.Ok("API is running")).AllowAnonymous();

app.MapGet("/api/config", (IConfiguration config) => 
{
    var allowPublic = config.GetValue<bool>("ALLOW_PUBLIC_REGISTRATION");
    return Results.Ok(new { allowPublic });
}).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

// Auth Endpoints
app.MapPost("/api/auth/login", async (ClaimsPrincipal user, LogService log) =>
{
    var userType = user.FindFirstValue("UserType");
    var canViewData = user.FindFirstValue("CanViewData") == "true";
    var canAddUsers = user.FindFirstValue("CanAddUsers") == "true";
    var canManageVolunteers = user.FindFirstValue("CanManageVolunteers") == "true";

    await log.LogAsync($"Login successful as {userType}");

    return Results.Ok(new { userType, canViewData, canAddUsers, canManageVolunteers });
}).RequireAuthorization();

app.MapPost("/api/auth/qr-login", async (AppDbContext db, LogService log, QrLoginRequest request) =>
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

// Registration Endpoints
app.MapPost("/api/members/public", async (AppDbContext db, CsvService csv, LogService log, IConfiguration config, MemberRegistrationDto dto) =>
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
        DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc),
        Guardian1Name = dto.Guardian1Name,
        Guardian1Phone = dto.Guardian1Phone,
        Guardian2Name = dto.Guardian2Name,
        Guardian2Phone = dto.Guardian2Phone,
        UserEmail = dto.UserEmail,
        Notes = dto.Notes,
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
    _ = csv.SyncMembersAsync();
    
    await log.LogAsync($"Public registration successful: {member.Username} ({member.MemberId})");
    
    return Results.Created($"/api/members/{member.MemberId}", member);
}).AllowAnonymous();

app.MapPost("/api/members", async (AppDbContext db, CsvService csv, LogService log, MemberRegistrationDto dto) =>
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
        DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc),
        Guardian1Name = dto.Guardian1Name,
        Guardian1Phone = dto.Guardian1Phone,
        Guardian2Name = dto.Guardian2Name,
        Guardian2Phone = dto.Guardian2Phone,
        UserEmail = dto.UserEmail,
        Notes = dto.Notes,
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
    _ = csv.SyncMembersAsync(); // Background sync
    
    await log.LogAsync($"Registered new member: {member.Username} ({member.MemberId})");

    return Results.Created($"/api/members/{member.MemberId}", member);
}).RequireAuthorization("CanAddUsers");

app.MapGet("/api/members", async (AppDbContext db, string? search) =>
{
    var query = db.Members.AsQueryable();
    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(c => EF.Functions.ILike(c.FirstName, $"%{search}%") || EF.Functions.ILike(c.LastName, $"%{search}%"));
    }
    return Results.Ok(await query.ToListAsync());
}).RequireAuthorization("CanViewData");

app.MapGet("/api/members/{id}", async (AppDbContext db, LogService log, Guid id) =>
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
        member.Username,
        member.QrSecret,
        AcceptsNewsletter = preference?.AcceptsNewsletter ?? false,
        AcceptsSurveys = preference?.AcceptsSurveys ?? false
    };

    return Results.Ok(result);
}).RequireAuthorization("CanViewData");

app.MapGet("/api/members/export", async (AppDbContext db) =>
{
    var members = await db.Members.ToListAsync();
    
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("MemberId,Username,FirstName,LastName,DateOfBirth,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,UserEmail,Notes");

    foreach (var m in members)
    {
        csv.AppendLine($"{m.MemberId},{m.Username},{m.FirstName},{m.LastName},{m.DateOfBirth:yyyy-MM-dd},\"{m.Guardian1Name}\",\"{m.Guardian1Phone}\",\"{m.Guardian2Name}\",\"{m.Guardian2Phone}\",{m.UserEmail},\"{m.Notes?.Replace("\"", "'")}\"");
    }

    var directory = Path.Combine(Directory.GetCurrentDirectory(), "drive");
    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
    
    var fileName = $"members_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
    var filePath = Path.Combine(directory, fileName);
    
    await File.WriteAllTextAsync(filePath, csv.ToString());
    
    return Results.Ok(new { fileName, filePath = "/app/drive/" + fileName });
}).RequireAuthorization("CanManageVolunteers");

app.MapGet("/api/attendance/export", async (AppDbContext db, DateTime? date) =>
{
    var targetDate = date?.Date ?? DateTime.UtcNow.Date;
    var nextDay = targetDate.AddDays(1);

    var logs = await db.AttendanceLogs
        .Include(l => l.Member)
        .Where(l => l.CheckInTime >= targetDate && l.CheckInTime < nextDay)
        .OrderBy(l => l.CheckInTime)
        .ToListAsync();
    
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("LogId,MemberId,Username,FirstName,LastName,CheckInTime,CheckOutTime,Guardian1Name,Guardian1Phone,Guardian2Name,Guardian2Phone,Notes");

    foreach (var l in logs)
    {
        var m = l.Member!;
        csv.AppendLine($"{l.LogId},{l.MemberId},{m.Username},{m.FirstName},{m.LastName},{l.CheckInTime:yyyy-MM-dd HH:mm:ss},{l.CheckOutTime:yyyy-MM-dd HH:mm:ss},\"{m.Guardian1Name}\",\"{m.Guardian1Phone}\",\"{m.Guardian2Name}\",\"{m.Guardian2Phone}\",\"{m.Notes?.Replace("\"", "'")}\"");
    }

    var directory = Path.Combine(Directory.GetCurrentDirectory(), "drive");
    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
    
    var fileName = $"attendance_export_{targetDate:yyyyMMdd}_{DateTime.UtcNow:HHmmss}.csv";
    var filePath = Path.Combine(directory, fileName);
    
    await File.WriteAllTextAsync(filePath, csv.ToString());
    
    return Results.Ok(new { fileName, filePath = "/app/drive/" + fileName, recordCount = logs.Count });
}).RequireAuthorization("CanManageVolunteers");

app.MapPut("/api/members/{id}", async (AppDbContext db, CsvService csv, LogService log, Guid id, MemberRegistrationDto dto) =>
{
    var member = await db.Members.FindAsync(id);
    if (member == null) return Results.NotFound();

    if (await db.Members.AnyAsync(m => m.MemberId != id && m.Username.ToLower() == dto.Username.ToLower()))
    {
        await log.LogAsync($"Failed profile update for {member.Username}: Username {dto.Username} already exists", true);
        return Results.BadRequest("Username already exists.");
    }

    // Update member details
    member.FirstName = dto.FirstName;
    member.LastName = dto.LastName;
    member.DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc);
    member.Guardian1Name = dto.Guardian1Name;
    member.Guardian1Phone = dto.Guardian1Phone;
    member.Guardian2Name = dto.Guardian2Name;
    member.Guardian2Phone = dto.Guardian2Phone;
    member.UserEmail = dto.UserEmail;
    member.Notes = dto.Notes;
    member.Username = dto.Username;
    
    if (!string.IsNullOrEmpty(dto.Password) && dto.Password != "********")
    {
        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
    }

    // Update preferences
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
    _ = csv.SyncMembersAsync(); // Background sync
    
    await log.LogAsync($"Updated member profile: {member.Username} ({member.MemberId})");

    return Results.NoContent();
}).RequireAuthorization("CanAddUsers");

app.MapPost("/api/members/{id}/regenerate-qr", async (AppDbContext db, LogService log, Guid id) =>
{
    var member = await db.Members.FindAsync(id);
    if (member == null) return Results.NotFound();

    member.QrSecret = Guid.NewGuid();
    await db.SaveChangesAsync();

    await log.LogAsync($"Regenerated QR Secret for member: {member.Username} ({member.MemberId})");

    return Results.Ok(new { qrSecret = member.QrSecret });
}).RequireAuthorization("CanAddUsers");

app.MapPut("/api/members/self/password", async (AppDbContext db, LogService log, UpdatePasswordDto dto, ClaimsPrincipal user) =>
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

// Attendance Endpoints
app.MapPost("/api/attendance/check-in", async (AppDbContext db, CsvService csv, LogService log, CheckInRequest request) =>
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
    _ = csv.SyncAttendanceAsync(DateTime.UtcNow);
    
    await log.LogAsync(logMsg);
    
    return Results.Ok(attendanceLog);
}).RequireAuthorization("CanViewData");

app.MapPut("/api/attendance/check-out", async (AppDbContext db, CsvService csv, LogService log, CheckOutRequest request) =>
{
    var attendanceLog = await db.AttendanceLogs.Include(l => l.Member).FirstOrDefaultAsync(l => l.LogId == request.LogId);
    if (attendanceLog == null) return Results.NotFound();
    
    attendanceLog.CheckOutTime = DateTime.UtcNow;
    await db.SaveChangesAsync();
    _ = csv.SyncAttendanceAsync(attendanceLog.CheckInTime);
    
    await log.LogAsync($"Volunteer checked out member: {attendanceLog.Member?.Username}");

    return Results.Ok(attendanceLog);
}).RequireAuthorization("CanViewData");

app.MapPost("/api/attendance/self-check-in", async (AppDbContext db, CsvService csv, LogService log, ClaimsPrincipal user) =>
{
    var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
    var memberId = Guid.Parse(memberIdStr);

    var attendanceLog = new AttendanceLog
    {
        MemberId = memberId,
        CheckInTime = DateTime.UtcNow
    };
    db.AttendanceLogs.Add(attendanceLog);
    await db.SaveChangesAsync();
    _ = csv.SyncAttendanceAsync(DateTime.UtcNow);
    
    await log.LogAsync("Member self-checked in");

    return Results.Ok(attendanceLog);
}).RequireAuthorization("IsMember");

app.MapPost("/api/attendance/self-check-out", async (AppDbContext db, CsvService csv, LogService log, ClaimsPrincipal user) =>
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
    _ = csv.SyncAttendanceAsync(attendanceLog.CheckInTime);
    
    await log.LogAsync("Member self-checked out");

    return Results.Ok(attendanceLog);
}).RequireAuthorization("IsMember");

app.MapPost("/api/attendance/qr-action", async (AppDbContext db, CsvService csv, LogService log, QrLoginRequest request) =>
{
    var member = await db.Members.SingleOrDefaultAsync(m => m.Username.ToLower() == request.Username.ToLower() && m.QrSecret == request.QrSecret);
    
    if (member == null)
    {
        await log.LogAsync($"Invalid QR action attempt for username: {request.Username}", true);
        return Results.Unauthorized();
    }

    var activeLog = await db.AttendanceLogs
        .Where(l => l.MemberId == member.MemberId && l.CheckOutTime == null)
        .OrderByDescending(l => l.CheckInTime)
        .FirstOrDefaultAsync();

    string action;
    if (activeLog == null)
    {
        // Perform Check-In
        var logEntry = new AttendanceLog
        {
            MemberId = member.MemberId,
            CheckInTime = DateTime.UtcNow
        };
        db.AttendanceLogs.Add(logEntry);
        action = "CheckedIn";
        await log.LogAsync($"QR Direct Check-In: {member.Username}");
    }
    else
    {
        // Perform Check-Out
        activeLog.CheckOutTime = DateTime.UtcNow;
        action = "CheckedOut";
        await log.LogAsync($"QR Direct Check-Out: {member.Username}");
    }

    await db.SaveChangesAsync();
    _ = csv.SyncAttendanceAsync(DateTime.UtcNow);

    return Results.Ok(new { action, username = member.Username, firstName = member.FirstName, lastName = member.LastName });
}).AllowAnonymous();

app.MapGet("/api/attendance/self-status", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var memberIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(memberIdStr)) return Results.Unauthorized();
    var memberId = Guid.Parse(memberIdStr);

    var isActive = await db.AttendanceLogs
        .AnyAsync(l => l.MemberId == memberId && l.CheckOutTime == null);

    return Results.Ok(new { isActive });
}).RequireAuthorization("IsMember");

app.MapGet("/api/attendance/active", async (AppDbContext db) =>
{
    var activeAttendance = await db.AttendanceLogs
        .Include(l => l.Member)
        .Where(l => l.CheckOutTime == null)
        .Select(l => new
        {
            l.MemberId,
            l.Member!.FirstName,
            l.Member!.LastName,
            l.Member!.Notes,
            l.Member!.Guardian1Phone,
            l.Member!.Guardian2Phone,
            l.CheckInTime

        })
        .ToListAsync();
    return Results.Ok(activeAttendance);
}).RequireAuthorization("CanViewData");

app.MapGet("/api/attendance/history", async (AppDbContext db, DateTime? date) =>
{
    var targetDate = date?.Date ?? DateTime.UtcNow.Date;
    var nextDay = targetDate.AddDays(1);

    var history = await db.AttendanceLogs
        .Include(l => l.Member)
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
            l.Member!.Guardian1Phone
        })
        .ToListAsync();
    return Results.Ok(history);
}).RequireAuthorization("CanViewData");

app.MapGet("/api/logs", async (AppDbContext db) =>
{
    var logs = await db.SystemLogs
        .OrderByDescending(l => l.Timestamp)
        .Take(500)
        .ToListAsync();
    return Results.Ok(logs);
}).RequireAuthorization("CanManageVolunteers");

// Volunteer Management Endpoints
app.MapGet("/api/volunteers", async (AppDbContext db) =>
{
    return Results.Ok(await db.Volunteers.Select(v => new { v.VolunteerId, v.Username, v.CanViewData, v.CanAddUsers, v.CanManageVolunteers }).ToListAsync());
}).RequireAuthorization("CanManageVolunteers");

app.MapPost("/api/volunteers", async (AppDbContext db, CreateVolunteerDto dto) =>
{
    if (await db.Volunteers.AnyAsync(v => v.Username.ToLower() == dto.Username.ToLower()))
        return Results.BadRequest("Username already exists.");

    var volunteer = new Volunteer
    {
        Username = dto.Username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        CanViewData = dto.CanViewData,
        CanAddUsers = dto.CanAddUsers,
        CanManageVolunteers = dto.CanManageVolunteers
    };
    db.Volunteers.Add(volunteer);
    await db.SaveChangesAsync();
    return Results.Created($"/api/volunteers/{volunteer.VolunteerId}", new { volunteer.VolunteerId, volunteer.Username });
}).RequireAuthorization("CanManageVolunteers");

app.MapPut("/api/volunteers/{id}/password", async (AppDbContext db, Guid id, UpdatePasswordDto dto, ClaimsPrincipal user) =>
{
    var volunteer = await db.Volunteers.FindAsync(id);
    if (volunteer == null) return Results.NotFound();

    var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var isSelf = currentUserId == id.ToString();
    var isManager = user.HasClaim("CanManageVolunteers", "true");

    if (!isSelf && !isManager) return Results.Forbid();

    volunteer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPut("/api/volunteers/{id}/permissions", async (AppDbContext db, Guid id, UpdatePermissionsDto dto) =>
{
    var volunteer = await db.Volunteers.FindAsync(id);
    if (volunteer == null) return Results.NotFound();

    volunteer.CanViewData = dto.CanViewData;
    volunteer.CanAddUsers = dto.CanAddUsers;
    volunteer.CanManageVolunteers = dto.CanManageVolunteers;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("CanManageVolunteers");

app.MapDelete("/api/volunteers/{id}", async (AppDbContext db, Guid id, ClaimsPrincipal user) =>
{
    var volunteer = await db.Volunteers.FindAsync(id);
    if (volunteer == null) return Results.NotFound();

    if (user.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
        return Results.BadRequest("Cannot delete your own account.");

    db.Volunteers.Remove(volunteer);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("CanManageVolunteers");

// Ensure database is created and seed initial volunteer if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
    if (!db.Volunteers.Any())
    {
        db.Volunteers.Add(new Volunteer
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            CanViewData = true,
            CanAddUsers = true,
            CanManageVolunteers = true
        });
        db.SaveChanges();
    }
}

app.Run();

// DTOs
public record CheckInRequest(Guid MemberId);
public record CheckOutRequest(Guid LogId);

public record CreateVolunteerDto(string Username, string Password, bool CanViewData, bool CanAddUsers, bool CanManageVolunteers);
public record UpdatePasswordDto(string NewPassword);
public record UpdatePermissionsDto(bool CanViewData, bool CanAddUsers, bool CanManageVolunteers);

public record QrLoginRequest(string Username, Guid QrSecret);
