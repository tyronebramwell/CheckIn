namespace CheckInCommon.Models;

[Obsolete("Use the new models for the Charity Event system.")]
public class Reasons
{
    public string Name { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}
