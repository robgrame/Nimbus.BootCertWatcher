namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the confidence level for firmware compatibility with Secure Boot updates.
    /// Based on the firmware release date, this indicates how likely the firmware is to 
    /// support the latest Secure Boot certificate requirements.
    /// </summary>
    public enum FirmwareConfidenceLevel
    {
        /// <summary>
        /// Unknown - Firmware release date is not available.
        /// Unable to determine firmware compatibility confidence.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Low confidence - Firmware released before 2024.
        /// May lack support for modern Secure Boot certificate requirements.
        /// Firmware update strongly recommended before proceeding with Secure Boot certificate update.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium confidence - Firmware released during 2024.
        /// Likely supports Secure Boot certificate updates, but verification recommended.
        /// Some early 2024 firmware may require updates for full compatibility.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High confidence - Firmware released after January 1, 2025.
        /// Expected to fully support latest Secure Boot certificate requirements.
        /// These firmware versions were released after the certificate update announcements.
        /// </summary>
        High = 3
    }

    /// <summary>
    /// Extension methods for FirmwareConfidenceLevel.
    /// </summary>
    public static class FirmwareConfidenceLevelExtensions
    {
        /// <summary>
        /// Gets the CSS color class for the confidence level (Bootstrap compatible).
        /// </summary>
        public static string GetColorClass(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => "success",    // Green
                FirmwareConfidenceLevel.Medium => "warning",  // Yellow/Amber
                FirmwareConfidenceLevel.Low => "danger",      // Red
                _ => "secondary"                              // Gray for Unknown
            };
        }

        /// <summary>
        /// Gets the hex color code for the confidence level.
        /// </summary>
        public static string GetHexColor(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => "#28a745",    // Green
                FirmwareConfidenceLevel.Medium => "#ffc107",  // Yellow/Amber
                FirmwareConfidenceLevel.Low => "#dc3545",     // Red
                _ => "#6c757d"                                // Gray for Unknown
            };
        }

        /// <summary>
        /// Gets the Font Awesome icon class for the confidence level.
        /// </summary>
        public static string GetIconClass(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => "fa-solid fa-circle-check",      // Green checkmark
                FirmwareConfidenceLevel.Medium => "fa-solid fa-triangle-exclamation", // Yellow warning
                FirmwareConfidenceLevel.Low => "fa-solid fa-circle-xmark",       // Red X
                _ => "fa-solid fa-circle-question"                               // Gray question mark
            };
        }

        /// <summary>
        /// Gets the display name for the confidence level.
        /// </summary>
        public static string GetDisplayName(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => "High",
                FirmwareConfidenceLevel.Medium => "Medium",
                FirmwareConfidenceLevel.Low => "Low",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Gets a detailed description of what the confidence level means.
        /// </summary>
        public static string GetDescription(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => 
                    "Firmware released after Jan 1, 2025. Expected to fully support Secure Boot certificate updates.",
                FirmwareConfidenceLevel.Medium => 
                    "Firmware released during 2024. Likely supports updates, but verification is recommended.",
                FirmwareConfidenceLevel.Low => 
                    "Firmware released before 2024. Firmware update strongly recommended before proceeding.",
                _ => 
                    "Firmware release date unknown. Unable to assess compatibility confidence."
            };
        }

        /// <summary>
        /// Gets the emoji indicator for the confidence level.
        /// </summary>
        public static string GetEmoji(this FirmwareConfidenceLevel level)
        {
            return level switch
            {
                FirmwareConfidenceLevel.High => "?",
                FirmwareConfidenceLevel.Medium => "??",
                FirmwareConfidenceLevel.Low => "?",
                _ => "?"
            };
        }
    }
}
