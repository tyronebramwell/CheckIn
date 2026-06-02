using CheckInApi.Data;
using CheckInApi.Models;
using CheckInApi.Services;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events");

        group.MapGet("/", async (AppDbContext db) =>
        {
            return await db.Events.OrderByDescending(e => e.EventDate).ToListAsync();
        }).RequireAuthorization("CanViewData");

        group.MapGet("/today", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return await db.Events.Where(e => e.EventDate == today).ToListAsync();
        }).AllowAnonymous();

        group.MapPost("/", async (AppDbContext db, LogService log, CreateEventDto dto) =>
        {
            var eventsToCreate = new List<Event>();
            var currentDate = dto.EventDate;

            // First instance
            eventsToCreate.Add(new Event { Name = dto.Name, EventDate = currentDate });

            if (dto.RepeatCount > 0 && dto.RepeatType != "None")
            {
                for (int i = 0; i < dto.RepeatCount; i++)
                {
                    currentDate = dto.RepeatType switch
                    {
                        "Weekly" => currentDate.AddDays(7),
                        "Bi-Weekly" => currentDate.AddDays(14),
                        "Monthly" => currentDate.AddMonths(1),
                        _ => currentDate
                    };
                    eventsToCreate.Add(new Event { Name = dto.Name, EventDate = currentDate });
                }
            }

            db.Events.AddRange(eventsToCreate);
            await db.SaveChangesAsync();
            
            await log.LogAsync($"Created {eventsToCreate.Count} events: {dto.Name} starting {dto.EventDate:yyyy-MM-dd}");
            return Results.Ok(eventsToCreate);
        }).RequireAuthorization("CanManageEvents");

        group.MapDelete("/{id}", async (AppDbContext db, LogService log, Guid id) =>
        {
            var ev = await db.Events.FindAsync(id);
            if (ev == null) return Results.NotFound();
            
            db.Events.Remove(ev);
            await db.SaveChangesAsync();
            await log.LogAsync($"Deleted event: {ev.Name}");
            return Results.NoContent();
        }).RequireAuthorization("CanManageEvents");
    }
}
