using System.ComponentModel.DataAnnotations;

namespace CheckInCommon.Models;

public class Volunteer
{
    [Key]
    public Guid VolunteerId { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    public bool CanViewData { get; set; }
    
    public bool CanAddUsers { get; set; }
    
    public bool CanManageVolunteers { get; set; }
}
