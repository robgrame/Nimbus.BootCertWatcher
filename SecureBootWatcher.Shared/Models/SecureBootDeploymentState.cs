namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the high-level Secure Boot certificate deployment state inferred from registry values.
    /// </summary>
    public enum SecureBootDeploymentState
    {
        Unknown = 0,
        NotStarted,
        InProgress,
        Updated,
        Error
    }
}
