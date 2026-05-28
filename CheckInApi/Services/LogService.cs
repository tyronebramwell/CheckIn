using CheckInApi.Data;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CheckInApi.Services;

public class LogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LogService(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, bool isError = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var context = _httpContextAccessor.HttpContext;
        var user = context?.User?.Identity?.Name ?? "Anonymous";
        var ip = context?.Connection?.RemoteIpAddress?.ToString();
        var deviceInfo = context?.Request?.Headers["User-Agent"].ToString();

        var log = new SystemLog
        {
            Timestamp = DateTime.UtcNow,
            User = user,
            Action = action,
            IsError = isError,
            IpAddress = ip,
            DeviceInfo = deviceInfo
        };

        db.SystemLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
