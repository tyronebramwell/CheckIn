using System.ComponentModel.DataAnnotations;

namespace CheckInCommon.Models;

public class Event
{
    [Key]
    public Guid EventId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateOnly EventDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
