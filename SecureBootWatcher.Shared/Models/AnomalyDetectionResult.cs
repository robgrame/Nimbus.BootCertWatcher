using System;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the result of anomaly detection analysis
    /// </summary>
    public sealed class AnomalyDetectionResult
    {
        public Guid Id { get; set; }
        
        public Guid DeviceId { get; set; }
        
        public string DeviceName { get; set; } = string.Empty;
        
        public AnomalyType Type { get; set; }
        
        public string Description { get; set; } = string.Empty;
        
        public double Severity { get; set; }
        
        public DateTimeOffset DetectedAtUtc { get; set; }
        
        public AnomalyStatus Status { get; set; }
        
        public string? ResolvedBy { get; set; }
        
        public DateTimeOffset? ResolvedAtUtc { get; set; }
        
        public string? Metadata { get; set; }
    }
    
    public enum AnomalyType
    {
        DeviceBehavior,
        CertificateChange,
        DeploymentState,
        ReportingFrequency,
        FleetWide
    }
    
    public enum AnomalyStatus
    {
        Active,
        Investigating,
        Resolved,
        FalsePositive
    }
}
