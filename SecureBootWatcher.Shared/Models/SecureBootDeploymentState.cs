namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the high-level Secure Boot certificate deployment state inferred from registry values.
    /// </summary>
    public enum SecureBootDeploymentState
    { 
        NotStarted,
        InProgress,
        Updated,
        Error,
        Unknown
    }
}
