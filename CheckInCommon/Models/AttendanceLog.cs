using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CheckInCommon.Models;

public class AttendanceLog
{
    [Key]
    public Guid LogId { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid MemberId { get; set; }
    
    [ForeignKey("MemberId")]
    public Member? Member { get; set; }
    
    [Required]
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? CheckOutTime { get; set; }
}
