using System;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Entity for storing detected anomalies
    /// </summary>
    public sealed class AnomalyEntity
    {
        public Guid Id { get; set; }
        
        public Guid DeviceId { get; set; }
        
        public DeviceEntity? Device { get; set; }
        
        public string AnomalyType { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public double Severity { get; set; }
        
        public DateTimeOffset DetectedAtUtc { get; set; }
        
        public string Status { get; set; } = "Active";
        
        public string? ResolvedBy { get; set; }
        
        public DateTimeOffset? ResolvedAtUtc { get; set; }
        
        public string? Metadata { get; set; }
    }
}
