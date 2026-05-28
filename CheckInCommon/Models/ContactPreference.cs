using System.ComponentModel.DataAnnotations;

namespace CheckInCommon.Models;

public class ContactPreference
{
    [Key]
    [MaxLength(255)]
    [EmailAddress]
    public string UserEmail { get; set; } = string.Empty;
    
    public bool AcceptsNewsletter { get; set; }
    
    public bool AcceptsSurveys { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
