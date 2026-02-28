using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for evaluating Secure Boot readiness based on certificates, OS version, and firmware.
    /// </summary>
    public interface ISecureBootReadinessService
    {
        /// <summary>
        /// Evaluates device readiness for Secure Boot certificate updates.
        /// </summary>
        ReadinessEvaluation EvaluateReadiness(
            SecureBootCertificateCollection? certificates,
            string? osVersion,
            string? osBuildNumber,
            DateTime? firmwareReleaseDate = null);
    }

    public sealed class SecureBootReadinessService : ISecureBootReadinessService
    {
        private readonly SecureBootReadinessOptions _options;
        private readonly ILogger<SecureBootReadinessService> _logger;

        public SecureBootReadinessService(
            IOptions<SecureBootReadinessOptions> options,
            ILogger<SecureBootReadinessService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public ReadinessEvaluation EvaluateReadiness(
            SecureBootCertificateCollection? certificates,
            string? osVersion,
            string? osBuildNumber,
            DateTime? firmwareReleaseDate = null)
        {
            var evaluation = new ReadinessEvaluation();

            // Evaluate OS version readiness
            evaluation.IsOSReady = EvaluateOSVersion(osVersion, osBuildNumber);
            evaluation.OSEvaluationDetails = GetOSEvaluationDetails(osVersion, osBuildNumber);

            // Evaluate firmware confidence based on release date
            EvaluateFirmwareConfidence(firmwareReleaseDate, evaluation);

            // Evaluate certificate readiness
            if (certificates != null)
            {
                EvaluateCertificates(certificates, evaluation);
            }
            else
            {
                evaluation.AreOemCertificatesValid = false;
                evaluation.HasWindowsUEFICA2023 = false;
                evaluation.CertificateEvaluationDetails = "No certificate data available";
            }

            // Overall readiness - now includes firmware confidence consideration
            // A device is ready to update if:
            // 1. OS version meets minimum requirements
            // 2. OEM certificates are valid (not expired/critical)
            // 3. Firmware certificates (KEK/PK) are valid (not expired/critical)
            // 4. Firmware confidence is not LOW (or firmware date is unknown - we allow it with warning)
            // 
            // Note: Windows UEFI CA 2023 is not required for readiness as it gets provisioned
            // AFTER the secure boot certificate upgrade, not before.
            bool firmwareAcceptable = evaluation.FirmwareConfidence != FirmwareConfidenceLevel.Low;
            
            evaluation.IsReadyToUpdate = evaluation.IsOSReady &&
                                         evaluation.AreOemCertificatesValid &&
                                         evaluation.AreFirmwareCertificatesValid &&
                                         firmwareAcceptable;

            return evaluation;
        }

        private bool EvaluateOSVersion(string? osVersion, string? osBuildNumber)
        {
            if (string.IsNullOrEmpty(osVersion))
            {
                _logger.LogDebug("OS version is null or empty");
                return false;
            }

            try
            {
                // OSVersion from client should be complete (4 parts: "10.0.22621.6060")
                // If it has only 3 parts (e.g., "10.0.22621"), it's from an old report
                var versionParts = osVersion.Split('.');
                if (versionParts.Length < 4)
                {
                    _logger.LogWarning(
                        "OS version has only {PartCount} parts: {OSVersion}. " +
                        "Expected 4 parts (Major.Minor.Build.UBR). " +
                        "This may be from an old report before UBR tracking was implemented.",
                        versionParts.Length, osVersion);
                }

                // Determine which minimum version to compare against
                var minimumVersionString = DetermineMinimumVersion(osVersion);
                if (minimumVersionString == null)
                {
                    _logger.LogDebug("Could not determine minimum version for OS: {OSVersion}", osVersion);
                    return false;
                }

                // Compare versions using custom 4-part version comparison
                var isReady = CompareVersionStrings(osVersion, minimumVersionString) >= 0;
                
                _logger.LogDebug(
                    "OS Version Check: Current={Current} ({PartCount} parts), Required={Required}, Ready={Ready}",
                    osVersion, versionParts.Length, minimumVersionString, isReady);

                return isReady;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate OS version: {OSVersion}", osVersion);
                return false;
            }
        }

        /// <summary>
        /// Compares two version strings that may have up to 4 parts (Major.Minor.Build.Revision).
        /// Returns: -1 if version1 < version2, 0 if equal, 1 if version1 > version2
        /// </summary>
        private int CompareVersionStrings(string version1, string version2)
        {
            var parts1 = ParseVersionParts(version1);
            var parts2 = ParseVersionParts(version2);

            // Compare each part
            for (int i = 0; i < 4; i++)
            {
                if (parts1[i] < parts2[i]) return -1;
                if (parts1[i] > parts2[i]) return 1;
            }

            return 0; // Equal
        }

        /// <summary>
        /// Parses a version string into a 4-part int array [Major, Minor, Build, Revision].
        /// Missing parts default to 0.
        /// </summary>
        private int[] ParseVersionParts(string version)
        {
            var parts = new int[4]; // [Major, Minor, Build, Revision]
            var segments = version.Split('.');

            for (int i = 0; i < Math.Min(segments.Length, 4); i++)
            {
                if (int.TryParse(segments[i], out int value))
                {
                    parts[i] = value;
                }
            }

            return parts;
        }

        private string? DetermineMinimumVersion(string osVersion)
        {
            if (string.IsNullOrEmpty(osVersion))
                return null;

            var parts = osVersion.Split('.');
            if (parts.Length < 3)
                return null;

            var major = parts[0];
            var minor = parts[1];
            var build = int.Parse(parts[2]);

            // Windows 11 versions based on build number ranges
            // Use >= comparison for forward compatibility
            if (major == "10" && minor == "0" && build >= 22000)
            {
                if (build >= 26200) // Windows 11 25H2 and newer
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows11_25H2");
                }
                else if (build >= 26100) // Windows 11 24H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows11_24H2");
                }
                else if (build >= 22631) // Windows 11 23H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows11_23H2");
                }
                else if (build >= 22621) // Windows 11 22H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows11_22H2");
                }
                else if (build >= 22000) // Windows 11 21H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows11_21H2");
                }
            }

            // Windows 10 (10.0.19xxx) - use >= for forward compatibility
            if (major == "10" && minor == "0")
            {
                if (build >= 19045) // Windows 10 22H2 and newer
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_22H2");
                }
                else if (build >= 19044) // Windows 10 21H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_21H2");
                }
                else if (build >= 19043) // Windows 10 21H1
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_21H1");
                }
                else if (build >= 19042) // Windows 10 20H2
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_20H2");
                }
                else if (build >= 19041) // Windows 10 2004
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_2004");
                }
                else if (build >= 18363) // Windows 10 1909
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1909");
                }
                else if (build >= 18362) // Windows 10 1903
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1903");
                }
                else if (build >= 17763) // Windows 10 1809
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1809");
                }
                else if (build >= 17134) // Windows 10 1803
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1803");
                }
                else if (build >= 16299) // Windows 10 1709
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1709");
                }
                else if (build >= 15063) // Windows 10 1703
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1703");
                }
                else if (build >= 14393) // Windows 10 1607
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1607");
                }
                else if (build >= 10586) // Windows 10 1511
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1511");
                }
                else if (build >= 10240) // Windows 10 1507
                {
                    return _options.MinimumOSBuildVersions.GetValueOrDefault("Windows10_1507");
                }
            }

            return null;
        }

        private string GetOSEvaluationDetails(string? osVersion, string? osBuildNumber)
        {
            if (string.IsNullOrEmpty(osVersion))
                return "❌ OS version not available";

            var minimumVersion = DetermineMinimumVersion(osVersion);
            if (minimumVersion == null)
                return $"❓ Unknown OS version: {osVersion}";

            try
            {
                // Check if OS version is incomplete (< 4 parts)
                var versionParts = osVersion.Split('.');
                var minVersionParts = minimumVersion.Split('.');
                
                // If current version has fewer parts than minimum, it's incomplete
                if (versionParts.Length < 4 && minVersionParts.Length == 4)
                {
                    return $"❌ OS version incomplete: {osVersion} (missing UBR/revision). " +
                           $"Full version required: {minimumVersion}. " +
                           $"Device needs to send updated report with complete version.";
                }

                var comparison = CompareVersionStrings(osVersion, minimumVersion);

                if (comparison >= 0)
                {
                    return $"✅ OS version {osVersion} meets requirements (>= {minimumVersion})";
                }
                else
                {
                    // If both have same number of parts, show standard comparison message
                    if (versionParts.Length == minVersionParts.Length)
                    {
                        return $"❌ OS version {osVersion} does not meet requirements. Minimum required: {minimumVersion}";
                    }
                    else
                    {
                        // Different number of parts - clarify it's incomplete
                        return $"❌ OS version {osVersion} is incomplete and below minimum. Required: {minimumVersion}";
                    }
                }
            }
            catch
            {
                return $"❌ Failed to parse OS version: {osVersion}";
            }
        }

        private void EvaluateFirmwareConfidence(DateTime? firmwareReleaseDate, ReadinessEvaluation evaluation)
        {
            // Date thresholds for firmware confidence levels
            // HIGH: Released on or after January 1, 2025
            // MEDIUM: Released during 2024 (Jan 1, 2024 to Dec 31, 2024)
            // LOW: Released before 2024
            var highConfidenceDate = new DateTime(2025, 1, 1);
            var mediumConfidenceStartDate = new DateTime(2024, 1, 1);

            if (!firmwareReleaseDate.HasValue)
            {
                evaluation.FirmwareConfidence = FirmwareConfidenceLevel.Unknown;
                evaluation.FirmwareEvaluationDetails = "❓ Firmware release date not available. Unable to assess firmware compatibility confidence.";
                
                _logger.LogDebug("Firmware release date is null - confidence level: Unknown");
                return;
            }

            var releaseDate = firmwareReleaseDate.Value;

            if (releaseDate >= highConfidenceDate)
            {
                // Released after Jan 1, 2025 - HIGH confidence
                evaluation.FirmwareConfidence = FirmwareConfidenceLevel.High;
                evaluation.FirmwareEvaluationDetails = $"✅ Firmware released on {releaseDate:yyyy-MM-dd} (after Jan 2025). " +
                    "High confidence: Expected to fully support Secure Boot certificate updates.";
                
                _logger.LogDebug(
                    "Firmware release date {ReleaseDate:yyyy-MM-dd} >= {HighDate:yyyy-MM-dd} - HIGH confidence",
                    releaseDate, highConfidenceDate);
            }
            else if (releaseDate >= mediumConfidenceStartDate)
            {
                // Released during 2024 - MEDIUM confidence
                evaluation.FirmwareConfidence = FirmwareConfidenceLevel.Medium;
                evaluation.FirmwareEvaluationDetails = $"⚠️ Firmware released on {releaseDate:yyyy-MM-dd} (during 2024). " +
                    "Medium confidence: Likely supports updates, but verification recommended.";
                
                _logger.LogDebug(
                    "Firmware release date {ReleaseDate:yyyy-MM-dd} in 2024 range - MEDIUM confidence",
                    releaseDate);
            }
            else
            {
                // Released before 2024 - LOW confidence
                evaluation.FirmwareConfidence = FirmwareConfidenceLevel.Low;
                evaluation.FirmwareEvaluationDetails = $"❌ Firmware released on {releaseDate:yyyy-MM-dd} (before 2024). " +
                    "Low confidence: Firmware update strongly recommended before proceeding with Secure Boot certificate update.";
                
                _logger.LogWarning(
                    "Firmware release date {ReleaseDate:yyyy-MM-dd} is before 2024 - LOW confidence. " +
                    "Firmware update recommended before Secure Boot certificate update.",
                    releaseDate);
            }
        }

        private void EvaluateCertificates(SecureBootCertificateCollection certificates, ReadinessEvaluation evaluation)
        {
            var now = DateTimeOffset.UtcNow;
            var warningThreshold = now.AddDays(_options.CertificateExpirationWarningDays);
            var criticalThreshold = now.AddDays(_options.CertificateExpirationCriticalDays);
            
            // Define the April 2026 deadline for legacy certificates
            var april2026Deadline = new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero);

            // Check for Windows UEFI CA 2023 in SignatureDatabase (db)
            var dbCertificates = certificates.SignatureDatabase ?? new List<SecureBootCertificate>();
            
            evaluation.HasWindowsUEFICA2023 = dbCertificates.Any(cert =>
                cert.Thumbprint?.Equals(_options.WindowsUEFICA2023Thumbprint, StringComparison.OrdinalIgnoreCase) == true ||
                cert.Subject?.Contains("Windows UEFI CA 2023", StringComparison.OrdinalIgnoreCase) == true);

            // Check for legacy Microsoft certificates expiring in 2026
            // These are certificates like "Microsoft Windows Production PCA 2011"
            var legacyCerts2026 = dbCertificates
                .Where(cert => cert.IsMicrosoftCertificate == true && 
                              cert.NotAfter.HasValue && 
                              cert.NotAfter.Value.Year == 2026 &&
                              (cert.Subject?.Contains("Production PCA 2011", StringComparison.OrdinalIgnoreCase) == true ||
                               cert.Subject?.Contains("Microsoft Corporation UEFI CA 2011", StringComparison.OrdinalIgnoreCase) == true))
                .ToList();

            evaluation.HasLegacyCertificatesExpiring2026 = legacyCerts2026.Any();
            evaluation.LegacyCertificateCount2026 = legacyCerts2026.Count;

            if (legacyCerts2026.Any())
            {
                _logger.LogInformation(
                    "Found {Count} legacy Microsoft certificate(s) expiring in 2026 that need update to Windows UEFI CA 2023",
                    legacyCerts2026.Count);
                
                foreach (var cert in legacyCerts2026)
                {
                    _logger.LogDebug(
                        "Legacy cert expiring 2026: {Subject}, Expires: {NotAfter}",
                        cert.Subject, cert.NotAfter);
                }
            }

            // NOTE: KEK (Key Exchange Keys) and DB (Signature Database) certificates that are expiring
            // will be updated during the Secure Boot certificate upgrade, so we do NOT evaluate them
            // as blockers for readiness. The upgrade process itself will replace:
            // - KEK: Microsoft Corporation KEK CA 2011 → Microsoft Corporation KEK 2K CA 2023
            // - DB: Microsoft Windows Production PCA 2011 → Windows UEFI CA 2023
            // - DB: Microsoft Corporation UEFI CA 2011 → Microsoft UEFI CA 2023
            //
            // IMPORTANT: WindowsUEFICA2023Capable registry key is NOT a firmware capability indicator.
            // It tracks whether "Windows UEFI CA 2023" certificate is present in the DB (values: 0, 1, or 2).
            // This key is for limited deployment scenarios only. Use UEFICA2023Status instead for
            // general readiness evaluation.

            // Evaluate Platform Key (PK) certificates ONLY
            // These are NOT updated by the Secure Boot upgrade and must be valid before upgrade
            var pkCertificates = certificates.PlatformKeys ?? new List<SecureBootCertificate>();
            EvaluatePlatformKeyCertificates(pkCertificates, evaluation, now, criticalThreshold);

            // Analyze OEM certificates
            var oemCertificates = dbCertificates
                .Where(cert => cert.IsMicrosoftCertificate == false && !string.IsNullOrEmpty(cert.Subject))
                .ToList();

            if (oemCertificates.Any())
            {
                var expiredCount = 0;
                var criticalCount = 0;
                var warningCount = 0;
                var validCount = 0;

                foreach (var cert in oemCertificates)
                {
                    if (cert.IsExpired)
                    {
                        expiredCount++;
                        _logger.LogDebug("Expired OEM certificate: {Subject}, Expired: {NotAfter}", 
                            cert.Subject, cert.NotAfter);
                    }
                    else if (cert.NotAfter.HasValue)
                    {
                        if (cert.NotAfter.Value < criticalThreshold)
                        {
                            criticalCount++;
                            _logger.LogDebug("Critical OEM certificate (expires < {Days} days): {Subject}, Expires: {NotAfter}",
                                _options.CertificateExpirationCriticalDays, cert.Subject, cert.NotAfter);
                        }
                        else if (cert.NotAfter.Value < warningThreshold)
                        {
                            warningCount++;
                            _logger.LogDebug("Warning OEM certificate (expires < {Days} days): {Subject}, Expires: {NotAfter}",
                                _options.CertificateExpirationWarningDays, cert.Subject, cert.NotAfter);
                        }
                        else
                        {
                            validCount++;
                        }
                    }
                }

                evaluation.ExpiredOemCertificateCount = expiredCount;
                evaluation.CriticalOemCertificateCount = criticalCount;
                evaluation.WarningOemCertificateCount = warningCount;
                evaluation.ValidOemCertificateCount = validCount;
                evaluation.HasNoOemCertificates = false;

                // OEM certificates are valid if none are expired or critical
                evaluation.AreOemCertificatesValid = expiredCount == 0 && criticalCount == 0;

                // Build detailed message
                var details = new List<string>();
                if (expiredCount > 0)
                    details.Add($"❌ {expiredCount} OEM certificate(s) expired");
                if (criticalCount > 0)
                    details.Add($"⚠️ {criticalCount} OEM certificate(s) expiring soon (< {_options.CertificateExpirationCriticalDays} days)");
                if (warningCount > 0)
                    details.Add($"⚠️ {warningCount} OEM certificate(s) need attention (< {_options.CertificateExpirationWarningDays} days)");
                if (validCount > 0)
                    details.Add($"✅ {validCount} OEM certificate(s) valid");

                evaluation.CertificateEvaluationDetails = string.Join("; ", details);
            }
            else
            {
                // No OEM certificates found - this is a warning condition, not automatically valid
                // Could indicate: VM, consumer device, firmware read error, or misconfiguration
                evaluation.AreOemCertificatesValid = false;
                evaluation.HasNoOemCertificates = true;
                evaluation.CertificateEvaluationDetails = "⚠️ No OEM certificates found - verify if this is expected (VM/consumer device) or indicates a firmware read error";
                
                _logger.LogWarning("No OEM certificates found in signature database - this may indicate a virtual machine, consumer device, or firmware read error");
            }

            // Add firmware certificate status
            if (!evaluation.AreFirmwareCertificatesValid)
            {
                evaluation.CertificateEvaluationDetails += "; ❌ Platform Key (PK) has expired or critical certificate(s)";
            }

            // Add Windows UEFI CA 2023 status (informational only, not required for readiness)
            if (!evaluation.HasWindowsUEFICA2023)
            {
                evaluation.CertificateEvaluationDetails += "; ℹ️ Windows UEFI CA 2023 not yet installed (will be installed during upgrade)";
            }
            else
            {
                evaluation.CertificateEvaluationDetails += "; ✅ Windows UEFI CA 2023 already present";
            }
            
            // Add legacy certificate warning if present
            if (evaluation.HasLegacyCertificatesExpiring2026)
            {
                evaluation.CertificateEvaluationDetails += $"; ⚠️ {evaluation.LegacyCertificateCount2026} legacy Microsoft certificate(s) expiring April 2026 - update required";
            }
        }

        private void EvaluatePlatformKeyCertificates(
            IList<SecureBootCertificate> pkCertificates,
            ReadinessEvaluation evaluation,
            DateTimeOffset now,
            DateTimeOffset criticalThreshold)
        {
            if (!pkCertificates.Any())
            {
                _logger.LogDebug("No Platform Key (PK) certificates found");
                return;
            }

            var expiredCount = 0;
            var criticalCount = 0;

            foreach (var cert in pkCertificates)
            {
                if (cert.IsExpired)
                {
                    expiredCount++;
                    _logger.LogError(
                        "Expired Platform Key (PK) certificate: {Subject}, Expired on: {NotAfter}. " +
                        "PK is not updated by Secure Boot upgrade and must be valid before proceeding.",
                        cert.Subject, cert.NotAfter);
                }
                else if (cert.NotAfter.HasValue && cert.NotAfter.Value < criticalThreshold)
                {
                    criticalCount++;
                    _logger.LogError(
                        "Critical Platform Key (PK) certificate (expires < {Days} days): {Subject}, Expires: {NotAfter}. " +
                        "PK is not updated by Secure Boot upgrade and must be renewed before proceeding.",
                        _options.CertificateExpirationCriticalDays, cert.Subject, cert.NotAfter);
                }
            }

            evaluation.ExpiredPlatformKeyCertificateCount = expiredCount;
            evaluation.CriticalPlatformKeyCertificateCount = criticalCount;

            // Mark firmware certificates as invalid if PK has expired or critical certificates
            if (expiredCount > 0 || criticalCount > 0)
            {
                evaluation.AreFirmwareCertificatesValid = false;
                _logger.LogError(
                    "Platform Key (PK) has {ExpiredCount} expired and {CriticalCount} critical certificate(s) - " +
                    "PK is not updated by Secure Boot upgrade and device is not ready",
                    expiredCount, criticalCount);
            }
        }
    }

    /// <summary>
    /// Result of readiness evaluation.
    /// </summary>
    public sealed class ReadinessEvaluation
    {
        /// <summary>
        /// Overall readiness status.
        /// Device is ready if OS, OEM certificates, and firmware confidence are all acceptable.
        /// </summary>
        public bool IsReadyToUpdate { get; set; }

        /// <summary>
        /// OS version meets minimum requirements.
        /// </summary>
        public bool IsOSReady { get; set; }

        /// <summary>
        /// OEM certificates are valid (not expired and not expiring soon).
        /// </summary>
        public bool AreOemCertificatesValid { get; set; }

        /// <summary>
        /// Windows UEFI CA 2023 is present in db.
        /// </summary>
        public bool HasWindowsUEFICA2023 { get; set; }

        /// <summary>
        /// Indicates if no OEM certificates were found (VM, consumer device, or read error).
        /// </summary>
        public bool HasNoOemCertificates { get; set; }

        /// <summary>
        /// Indicates if device has legacy Microsoft certificates (e.g., Windows Production PCA 2011) 
        /// that will expire in April 2026 and needs to be updated to Windows UEFI CA 2023
        /// </summary>
        public bool HasLegacyCertificatesExpiring2026 { get; set; }

        /// <summary>
        /// Number of legacy Microsoft certificates expiring in 2026
        /// </summary>
        public int LegacyCertificateCount2026 { get; set; }

        /// <summary>
        /// Firmware compatibility confidence level based on release date.
        /// HIGH: Released after Jan 1, 2025
        /// MEDIUM: Released during 2024
        /// LOW: Released before 2024
        /// UNKNOWN: Release date not available
        /// </summary>
        public FirmwareConfidenceLevel FirmwareConfidence { get; set; } = FirmwareConfidenceLevel.Unknown;

        /// <summary>
        /// Number of expired OEM certificates.
        /// </summary>
        public int ExpiredOemCertificateCount { get; set; }

        /// <summary>
        /// Number of OEM certificates expiring within critical threshold (default: 90 days).
        /// </summary>
        public int CriticalOemCertificateCount { get; set; }

        /// <summary>
        /// Number of OEM certificates expiring within warning threshold (default: 180 days).
        /// </summary>
        public int WarningOemCertificateCount { get; set; }

        /// <summary>
        /// Number of valid OEM certificates.
        /// </summary>
        public int ValidOemCertificateCount { get; set; }

        /// <summary>
        /// Number of expired certificates in Platform Keys (PK).
        /// Platform Keys are not updated by Secure Boot upgrade and must be valid before upgrade.
        /// </summary>
        public int ExpiredPlatformKeyCertificateCount { get; set; }

        /// <summary>
        /// Number of critical (expiring soon) certificates in Platform Keys (PK).
        /// Platform Keys are not updated by Secure Boot upgrade and must be valid before upgrade.
        /// </summary>
        public int CriticalPlatformKeyCertificateCount { get; set; }

        /// <summary>
        /// Indicates if Platform Keys (PK) are valid (not expired/critical).
        /// PK is not updated by Secure Boot upgrade, so it must be valid beforehand.
        /// </summary>
        public bool AreFirmwareCertificatesValid { get; set; } = true;

        /// <summary>
        /// Detailed OS evaluation message.
        /// </summary>
        public string OSEvaluationDetails { get; set; } = string.Empty;

        /// <summary>
        /// Detailed certificate evaluation message.
        /// </summary>
        public string CertificateEvaluationDetails { get; set; } = string.Empty;

        /// <summary>
        /// Detailed firmware evaluation message including confidence level explanation.
        /// </summary>
        public string FirmwareEvaluationDetails { get; set; } = string.Empty;
    }
}

