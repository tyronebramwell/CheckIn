using System.ComponentModel.DataAnnotations;

namespace CheckInCommon.Models;

public class MemberRegistrationDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Guardian1Name { get; set; } = "";
    public string Guardian1Phone { get; set; } = "";
    public string? Guardian2Name { get; set; }
    public string? Guardian2Phone { get; set; }
    public string UserEmail { get; set; } = "";
    public string? Notes { get; set; }
    public bool AllowEmail { get; set; }
    public bool AcceptsNewsletter { get; set; }
    public bool AcceptsSurveys { get; set; }
    [Required]
    public string Username { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
    public string? NewPassword { get; set; }
    public Guid QrSecret { get; set; }
}
