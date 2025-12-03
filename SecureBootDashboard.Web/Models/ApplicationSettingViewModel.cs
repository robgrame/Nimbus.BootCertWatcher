namespace SecureBootDashboard.Web.Models;

/// <summary>
/// View model for application settings management
/// </summary>
public sealed class ApplicationSettingViewModel
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string Category { get; set; }
    public required string ValueType { get; set; }
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
    public bool RequiresRestart { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    
    /// <summary>
    /// Display value (masked if sensitive)
    /// </summary>
    public string DisplayValue => IsSensitive ? "***MASKED***" : Value;
    
    /// <summary>
    /// Category badge color
    /// </summary>
    public string CategoryBadgeColor => Category switch
    {
        "QueueProcessor" => "primary",
        "ClientUpdate" => "success",
        "SecureBootReadiness" => "warning",
        _ => "secondary"
    };
}

/// <summary>
/// Request to update a setting
/// </summary>
public sealed class UpdateSettingRequest
{
    public required string Value { get; set; }
    public string? UpdatedBy { get; set; }
}
