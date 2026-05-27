using System.ComponentModel.DataAnnotations;

namespace SignUpCommon.Models;

public class Member
{
    [Key]
    public Guid MemberId { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    public DateTime DateOfBirth { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Guardian1Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Guardian1Phone { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Guardian2Name { get; set; }
    
    [MaxLength(20)]
    public string? Guardian2Phone { get; set; }
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string UserEmail { get; set; } = string.Empty;
    
    public string? Notes { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public Guid QrSecret { get; set; } = Guid.NewGuid();
}
