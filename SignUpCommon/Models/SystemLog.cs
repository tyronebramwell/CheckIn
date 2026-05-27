using System.ComponentModel.DataAnnotations;

namespace SignUpCommon.Models;

public class SystemLog
{
    [Key]
    public Guid LogId { get; set; } = Guid.NewGuid();
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(100)]
    public string? User { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Action { get; set; } = string.Empty;
    
    public bool IsError { get; set; }
    
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    public string? DeviceInfo { get; set; }
}
