namespace CheckInCommon.Models;

[Obsolete("Use the new models for the Charity Event system.")]
public class SignInRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ReasonText { get; set; } = string.Empty;
    public DateTime SignInTime { get; set; }
    public DateTime? SignOutTime { get; set; }
    public string ImageData { get; set; } = string.Empty;
}
