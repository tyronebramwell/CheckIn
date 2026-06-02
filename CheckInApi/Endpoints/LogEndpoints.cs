using CheckInApi.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Endpoints;

public static class LogEndpoints
{
    public static void MapLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/logs", async (AppDbContext db) =>
        {
            var logs = await db.SystemLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .ToListAsync();
            return Results.Ok(logs);
        }).RequireAuthorization("CanManageVolunteers");
    }
}
