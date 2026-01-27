using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SecureBootDashboard.Api.Services;

/// <summary>
/// Service for validating client certificates against database-driven configuration.
/// </summary>
public class CertificateValidationService : ICertificateValidationService
{
    private readonly SecureBootDbContext _context;
    private readonly ILogger<CertificateValidationService> _logger;

    public CertificateValidationService(
        SecureBootDbContext context,
        ILogger<CertificateValidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CertificateValidationResult> ValidateClientCertificateAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var result = new CertificateValidationResult();
        
        // Get current configuration
        var config = await GetConfigurationAsync(cancellationToken);
        if (config == null)
        {
            _logger.LogWarning("Mutual TLS configuration not found in database");
            return CertificateValidationResult.Failure("Mutual TLS configuration not found");
        }

        if (!config.Enabled)
        {
            _logger.LogDebug("Mutual TLS is disabled - skipping validation");
            return CertificateValidationResult.Success();
        }

        _logger.LogDebug("Validating client certificate: Subject={Subject}, Thumbprint={Thumbprint}",
            certificate.Subject, certificate.Thumbprint);

        // Store validation details
        result.ValidationDetails["Subject"] = certificate.Subject;
        result.ValidationDetails["Issuer"] = certificate.Issuer;
        result.ValidationDetails["Thumbprint"] = certificate.Thumbprint;
        result.ValidationDetails["NotBefore"] = certificate.NotBefore;
        result.ValidationDetails["NotAfter"] = certificate.NotAfter;

        // 1. Validate certificate validity period
        if (config.ValidateCertificateValidity)
        {
            var now = DateTimeOffset.UtcNow;
            var gracePeriod = TimeSpan.FromDays(config.ExpirationGracePeriodDays);
            
            if (certificate.NotBefore > now.DateTime)
            {
                result.Errors.Add($"Certificate not yet valid. Valid from: {certificate.NotBefore:yyyy-MM-dd}");
            }

            if (certificate.NotAfter < now.DateTime)
            {
                result.Errors.Add($"Certificate expired on: {certificate.NotAfter:yyyy-MM-dd}");
            }
            else if (certificate.NotAfter < now.Add(gracePeriod).DateTime)
            {
                var daysUntilExpiration = (certificate.NotAfter - now.DateTime).Days;
                result.Warnings.Add($"Certificate expires in {daysUntilExpiration} days (grace period: {config.ExpirationGracePeriodDays} days)");
                
                if (config.ExpirationGracePeriodDays > 0)
                {
                    result.Errors.Add($"Certificate within expiration grace period ({daysUntilExpiration} days remaining)");
                }
            }
        }

        // 2. Check for self-signed certificates
        if (!config.AllowSelfSignedCertificates)
        {
            if (certificate.Subject == certificate.Issuer)
            {
                result.Errors.Add("Self-signed certificates are not allowed");
            }
        }

        // 3. Validate Extended Key Usage (Client Authentication)
        if (config.RequireClientAuthEku)
        {
            var hasClientAuthEku = false;
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension ekuExtension)
                {
                    // Client Authentication OID: 1.3.6.1.5.5.7.3.2
                    hasClientAuthEku = ekuExtension.EnhancedKeyUsages
                        .Cast<Oid>()
                        .Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2");
                    break;
                }
            }

            if (!hasClientAuthEku)
            {
                result.Errors.Add("Certificate does not have Client Authentication Extended Key Usage (1.3.6.1.5.5.7.3.2)");
            }
        }

        // 4. Thumbprint allowlist validation
        if (config.EnableThumbprintAllowlist && !string.IsNullOrWhiteSpace(config.AllowedThumbprints))
        {
            var allowedThumbprints = config.AllowedThumbprints
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Replace(":", "").Replace(" ", "").ToUpperInvariant())
                .ToHashSet();

            var certThumbprint = certificate.Thumbprint.ToUpperInvariant();
            
            if (!allowedThumbprints.Contains(certThumbprint))
            {
                result.Errors.Add($"Certificate thumbprint not in allowlist: {certThumbprint}");
            }
            else
            {
                _logger.LogInformation("Certificate thumbprint validated against allowlist: {Thumbprint}", certThumbprint);
            }
        }

        // 5. Issuer allowlist validation (using database CAs)
        if (config.EnableIssuerAllowlist)
        {
            var trustedCAs = await GetTrustedCAsAsync(cancellationToken);
            
            if (trustedCAs.Count == 0)
            {
                result.Errors.Add("Issuer allowlist is enabled but no trusted CAs are configured");
            }
            else
            {
                var matchedCA = trustedCAs.FirstOrDefault(ca => 
                    ca.Thumbprint.Equals(GetIssuerThumbprint(certificate), StringComparison.OrdinalIgnoreCase) ||
                    ca.Subject.Equals(certificate.Issuer, StringComparison.OrdinalIgnoreCase));

                if (matchedCA != null)
                {
                    result.MatchedCA = matchedCA;
                    _logger.LogInformation("Certificate issuer matched trusted CA: {CAName} (Thumbprint: {Thumbprint})",
                        matchedCA.CommonName, matchedCA.Thumbprint);
                }
                else
                {
                    result.Errors.Add($"Certificate issuer not in trusted CA list: {certificate.Issuer}");
                }
            }
        }

        // 6. Certificate chain validation
        if (config.ValidateCertificateChain)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = config.CheckCertificateRevocation 
                ? X509RevocationMode.Online 
                : X509RevocationMode.NoCheck;
            
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(config.RevocationCheckTimeoutSeconds);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            // Add trusted CAs to extra store if issuer allowlist is enabled
            if (config.EnableIssuerAllowlist)
            {
                var trustedCAs = await GetTrustedCAsAsync(cancellationToken);
                foreach (var ca in trustedCAs)
                {
                    try
                    {
                        var caBytes = Convert.FromBase64String(ca.CertificateDataBase64);
                        var caCert = X509CertificateLoader.LoadCertificate(caBytes);
                        chain.ChainPolicy.ExtraStore.Add(caCert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load CA certificate: {CAName}", ca.CommonName);
                    }
                }
            }

            var isChainValid = chain.Build(certificate);
            
            if (!isChainValid)
            {
                var chainErrors = chain.ChainStatus
                    .Select(status => $"{status.Status}: {status.StatusInformation}")
                    .ToList();
                
                result.Errors.Add($"Certificate chain validation failed: {string.Join("; ", chainErrors)}");
                result.ValidationDetails["ChainErrors"] = chainErrors;
                
                if (config.EnableDetailedLogging)
                {
                    _logger.LogWarning("Certificate chain validation failed for {Subject}. Errors: {Errors}",
                        certificate.Subject, string.Join(", ", chainErrors));
                }
            }
            else
            {
                _logger.LogDebug("Certificate chain validated successfully");
            }
        }

        // 7. Certificate revocation check (if enabled separately)
        if (config.CheckCertificateRevocation && !config.ValidateCertificateChain)
        {
            // Standalone revocation check (not already done by chain validation)
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(config.RevocationCheckTimeoutSeconds);
            
            var isNotRevoked = chain.Build(certificate);
            
            if (!isNotRevoked)
            {
                var revocationErrors = chain.ChainStatus
                    .Where(s => s.Status.ToString().Contains("Revok", StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.StatusInformation)
                    .ToList();

                if (revocationErrors.Any())
                {
                    result.Errors.Add($"Certificate revocation check failed: {string.Join("; ", revocationErrors)}");
                }
            }
        }

        // Final validation result
        result.IsValid = result.Errors.Count == 0;

        if (config.EnableDetailedLogging)
        {
            _logger.LogInformation(
                "Certificate validation complete: Subject={Subject}, IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
                certificate.Subject, result.IsValid, result.Errors.Count, result.Warnings.Count);
        }

        return result;
    }

    public async Task<MutualTlsConfigEntity?> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MutualTlsConfig
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrustedCertificateAuthorityEntity>> GetTrustedCAsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.TrustedCertificateAuthorities
            .Where(ca => ca.IsEnabled)
            .OrderBy(ca => ca.CommonName)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrustedCertificateAuthorityEntity> AddTrustedCAAsync(
        byte[] certificateData,
        string? description,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        X509Certificate2 certificate;
        
        try
        {
            certificate = X509CertificateLoader.LoadCertificate(certificateData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse certificate data");
            throw new InvalidOperationException("Invalid certificate data", ex);
        }

        // Extract certificate details
        var commonName = GetCommonName(certificate.Subject);
        var thumbprint = certificate.Thumbprint;
        var thumbprint256 = GetSha256Thumbprint(certificate);

        // Check for duplicates
        var existing = await _context.TrustedCertificateAuthorities
            .FirstOrDefaultAsync(ca => ca.Thumbprint == thumbprint, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Certificate already exists: {commonName} (Thumbprint: {thumbprint})");
        }

        var entity = new TrustedCertificateAuthorityEntity
        {
            CommonName = commonName,
            Thumbprint = thumbprint,
            Thumbprint256 = thumbprint256,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            IsRootCa = certificate.Subject == certificate.Issuer,
            SerialNumber = certificate.SerialNumber,
            CertificateDataBase64 = Convert.ToBase64String(certificateData),
            IsEnabled = true,
            Description = description,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = createdBy
        };

        _context.TrustedCertificateAuthorities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added trusted CA: {CommonName} (Thumbprint: {Thumbprint}), Created by: {CreatedBy}",
            commonName, thumbprint, createdBy);

        return entity;
    }

    public async Task<bool> RemoveTrustedCAAsync(int caId, CancellationToken cancellationToken = default)
    {
        var ca = await _context.TrustedCertificateAuthorities.FindAsync(new object[] { caId }, cancellationToken);
        
        if (ca == null)
        {
            return false;
        }

        _context.TrustedCertificateAuthorities.Remove(ca);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed trusted CA: {CommonName} (ID: {Id})", ca.CommonName, caId);

        return true;
    }

    public async Task<bool> SetCAEnabledAsync(
        int caId,
        bool enabled,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var ca = await _context.TrustedCertificateAuthorities.FindAsync(new object[] { caId }, cancellationToken);
        
        if (ca == null)
        {
            return false;
        }

        ca.IsEnabled = enabled;
        ca.UpdatedAtUtc = DateTimeOffset.UtcNow;
        ca.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated CA enabled status: {CommonName} (ID: {Id}), Enabled: {Enabled}, Updated by: {UpdatedBy}",
            ca.CommonName, caId, enabled, updatedBy);

        return true;
    }

    public async Task<MutualTlsConfigEntity> UpdateConfigurationAsync(
        MutualTlsConfigEntity config,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetConfigurationAsync(cancellationToken);
        
        if (existing == null)
        {
            // Create new configuration
            config.CreatedAtUtc = DateTimeOffset.UtcNow;
            config.CreatedBy = updatedBy;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            config.UpdatedBy = updatedBy;

            _context.MutualTlsConfig.Add(config);
        }
        else
        {
            // Update existing configuration
            existing.Enabled = config.Enabled;
            existing.AllowSelfSignedCertificates = config.AllowSelfSignedCertificates;
            existing.CheckCertificateRevocation = config.CheckCertificateRevocation;
            existing.ValidateCertificateChain = config.ValidateCertificateChain;
            existing.RequireClientAuthEku = config.RequireClientAuthEku;
            existing.ValidateCertificateValidity = config.ValidateCertificateValidity;
            existing.ExpirationGracePeriodDays = config.ExpirationGracePeriodDays;
            existing.EnableThumbprintAllowlist = config.EnableThumbprintAllowlist;
            existing.AllowedThumbprints = config.AllowedThumbprints;
            existing.EnableIssuerAllowlist = config.EnableIssuerAllowlist;
            existing.EnableDetailedLogging = config.EnableDetailedLogging;
            existing.RevocationCheckTimeoutSeconds = config.RevocationCheckTimeoutSeconds;
            existing.ValidationNotes = config.ValidationNotes;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            existing.UpdatedBy = updatedBy;

            config = existing;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated mutual TLS configuration. Enabled: {Enabled}, Updated by: {UpdatedBy}",
            config.Enabled, updatedBy);

        return config;
    }

    /// <summary>
    /// Extracts the Common Name (CN) from a certificate subject distinguished name.
    /// </summary>
    private static string GetCommonName(string subject)
    {
        var parts = subject.Split(',', StringSplitOptions.TrimEntries);
        var cnPart = parts.FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
        return cnPart?.Substring(3) ?? subject;
    }

    /// <summary>
    /// Computes the SHA-256 thumbprint of a certificate.
    /// </summary>
    private static string GetSha256Thumbprint(X509Certificate2 certificate)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(certificate.RawData);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    /// <summary>
    /// Gets the thumbprint of the issuer certificate from the chain (if available).
    /// </summary>
    private static string GetIssuerThumbprint(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(certificate);

        if (chain.ChainElements.Count > 1)
        {
            return chain.ChainElements[1].Certificate.Thumbprint;
        }

        return string.Empty;
    }
}
