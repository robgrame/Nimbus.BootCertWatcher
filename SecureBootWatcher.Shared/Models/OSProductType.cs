namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Windows Operating System product type as reported by Win32_OperatingSystem.ProductType.
    /// </summary>
    public enum OSProductType
    {
        /// <summary>
        /// Unknown or not set.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Workstation (desktop OS).
        /// Examples: Windows 10 Pro, Windows 11 Home, Windows 10 Enterprise.
        /// </summary>
        Workstation = 1,

        /// <summary>
        /// Domain Controller.
        /// </summary>
        DomainController = 2,

        /// <summary>
        /// Server (non-DC).
        /// Examples: Windows Server 2019, Windows Server 2022.
        /// </summary>
        Server = 3
    }

    /// <summary>
    /// Extension methods for OSProductType.
    /// </summary>
    public static class OSProductTypeExtensions
    {
        /// <summary>
        /// Gets a user-friendly display name for the OS product type.
        /// </summary>
        public static string GetDisplayName(this OSProductType productType)
        {
            return productType switch
            {
                OSProductType.Workstation => "Workstation",
                OSProductType.DomainController => "Domain Controller",
                OSProductType.Server => "Server",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Gets a Font Awesome icon class for the OS product type.
        /// </summary>
        public static string GetIconClass(this OSProductType productType)
        {
            return productType switch
            {
                OSProductType.Workstation => "fa-desktop",
                OSProductType.DomainController => "fa-building",
                OSProductType.Server => "fa-server",
                _ => "fa-question-circle"
            };
        }

        /// <summary>
        /// Gets a Bootstrap color class for the OS product type.
        /// </summary>
        public static string GetColorClass(this OSProductType productType)
        {
            return productType switch
            {
                OSProductType.Workstation => "primary",
                OSProductType.DomainController => "warning",
                OSProductType.Server => "success",
                _ => "secondary"
            };
        }

        /// <summary>
        /// Determines if the OS is a server operating system (Server or Domain Controller).
        /// </summary>
        public static bool IsServerOS(this OSProductType productType)
        {
            return productType == OSProductType.Server || productType == OSProductType.DomainController;
        }
    }
}
