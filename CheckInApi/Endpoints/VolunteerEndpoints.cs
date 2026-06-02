using System.Security.Claims;
using CheckInApi.Data;
using CheckInApi.Models;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CheckInApi.Endpoints;

public static class VolunteerEndpoints
{
    public static void MapVolunteerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/volunteers");

        group.MapGet("/", async (AppDbContext db) =>
        {
            return Results.Ok(await db.Volunteers.Select(v => new { v.VolunteerId, v.Username, v.Email, v.CanViewData, v.CanAddUsers, v.CanManageVolunteers, v.CanManageEvents }).ToListAsync());
        }).RequireAuthorization("CanManageVolunteers");

        group.MapPost("/", async (AppDbContext db, CreateVolunteerDto dto) =>
        {
            if (await db.Volunteers.AnyAsync(v => v.Username.ToLower() == dto.Username.ToLower()))
                return Results.BadRequest("Username already exists.");

            var volunteer = new Volunteer
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                CanViewData = dto.CanViewData,
                CanAddUsers = dto.CanAddUsers,
                CanManageVolunteers = dto.CanManageVolunteers,
                CanManageEvents = dto.CanManageEvents
            };
            db.Volunteers.Add(volunteer);
            await db.SaveChangesAsync();
            return Results.Created($"/api/volunteers/{volunteer.VolunteerId}", new { volunteer.VolunteerId, volunteer.Username });
        }).RequireAuthorization("CanManageVolunteers");

        group.MapPut("/{id}/password", async (AppDbContext db, Guid id, UpdatePasswordDto dto, ClaimsPrincipal user) =>
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

        group.MapPut("/{id}/permissions", async (AppDbContext db, Guid id, UpdatePermissionsDto dto) =>
        {
            var volunteer = await db.Volunteers.FindAsync(id);
            if (volunteer == null) return Results.NotFound();

            volunteer.Email = dto.Email;
            volunteer.CanViewData = dto.CanViewData;
            volunteer.CanAddUsers = dto.CanAddUsers;
            volunteer.CanManageVolunteers = dto.CanManageVolunteers;
            volunteer.CanManageEvents = dto.CanManageEvents;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CanManageVolunteers");

        group.MapDelete("/{id}", async (AppDbContext db, Guid id, ClaimsPrincipal user) =>
        {
            var volunteer = await db.Volunteers.FindAsync(id);
            if (volunteer == null) return Results.NotFound();

            if (user.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
                return Results.BadRequest("Cannot delete your own account.");

            db.Volunteers.Remove(volunteer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("CanManageVolunteers");
    }
}
