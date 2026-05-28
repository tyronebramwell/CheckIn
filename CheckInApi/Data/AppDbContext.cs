using Microsoft.EntityFrameworkCore;
using CheckInCommon.Models;

namespace CheckInApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;
    public DbSet<ContactPreference> ContactPreferences { get; set; } = null!;
    public DbSet<Volunteer> Volunteers { get; set; } = null!;
    public DbSet<SystemLog> SystemLogs { get; set; } = null!;
    public DbSet<SystemConfig> SystemConfigs { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.MemberId);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<AttendanceLog>(entity =>
        {
            entity.HasKey(e => e.LogId);
            entity.HasOne(d => d.Member)
                .WithMany()
                .HasForeignKey(d => d.MemberId);
            
            entity.HasOne(d => d.Event)
                .WithMany()
                .HasForeignKey(d => d.EventId);
        });

        modelBuilder.Entity<ContactPreference>(entity =>
        {
            entity.HasKey(e => e.UserEmail);
            entity.Property(e => e.UserEmail).HasMaxLength(255);
        });

        modelBuilder.Entity<Volunteer>(entity =>
        {
            entity.HasKey(e => e.VolunteerId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
        });
        
        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });
    }
}
