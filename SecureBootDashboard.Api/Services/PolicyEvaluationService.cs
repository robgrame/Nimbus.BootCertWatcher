using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for evaluating device compliance against certificate policies.
    /// </summary>
    public interface IPolicyEvaluationService
    {
        /// <summary>
        /// Evaluates a device's certificates against active policies.
        /// </summary>
        ComplianceResult EvaluateCompliance(
            Guid deviceId,
            Guid? reportId,
            SecureBootCertificateCollection? certificates,
            string? fleetId);
    }

    public sealed class PolicyEvaluationService : IPolicyEvaluationService
    {
        private readonly IEnumerable<CertificateCompliancePolicy> _policies;

        public PolicyEvaluationService(IEnumerable<CertificateCompliancePolicy> policies)
        {
            _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }

        public ComplianceResult EvaluateCompliance(
            Guid deviceId,
            Guid? reportId,
            SecureBootCertificateCollection? certificates,
            string? fleetId)
        {
            var result = new ComplianceResult
            {
                DeviceId = deviceId,
                ReportId = reportId,
                EvaluatedAtUtc = DateTimeOffset.UtcNow,
                Status = ComplianceStatus.Compliant
            };

            if (certificates == null || certificates.TotalCertificateCount == 0)
            {
                result.Status = ComplianceStatus.Unknown;
                return result;
            }

            // Get applicable policies
            var applicablePolicies = _policies
                .Where(p => p.IsEnabled)
                .Where(p => string.IsNullOrEmpty(p.FleetId) || p.FleetId == fleetId)
                .OrderBy(p => p.Priority)
                .ToList();

            foreach (var policy in applicablePolicies)
            {
                EvaluatePolicy(policy, certificates, result);
            }

            // Determine overall status based on violations
            if (result.Violations.Any(v => v.Rule.Severity == PolicySeverity.Critical))
            {
                result.Status = ComplianceStatus.NonCompliant;
            }
            else if (result.Violations.Any(v => v.Rule.Severity == PolicySeverity.Warning))
            {
                result.Status = ComplianceStatus.Warning;
            }

            return result;
        }

        private void EvaluatePolicy(
            CertificateCompliancePolicy policy,
            SecureBootCertificateCollection certificates,
            ComplianceResult result)
        {
            foreach (var rule in policy.Rules)
            {
                EvaluateRule(policy, rule, certificates, result);
            }
        }

        private void EvaluateRule(
            CertificateCompliancePolicy policy,
            PolicyRule rule,
            SecureBootCertificateCollection certificates,
            ComplianceResult result)
        {
            // Gather all certificates from all databases
            var allCertificates = new List<SecureBootCertificate>();
            allCertificates.AddRange(certificates.SignatureDatabase ?? new List<SecureBootCertificate>());
            allCertificates.AddRange(certificates.ForbiddenDatabase ?? new List<SecureBootCertificate>());
            allCertificates.AddRange(certificates.KeyExchangeKeys ?? new List<SecureBootCertificate>());
            allCertificates.AddRange(certificates.PlatformKeys ?? new List<SecureBootCertificate>());

            // Apply database filter if specified
            var certsToCheck = allCertificates;
            if (!string.IsNullOrEmpty(rule.DatabaseFilter))
            {
                certsToCheck = certsToCheck
                    .Where(c => c.Database?.Equals(rule.DatabaseFilter, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            foreach (var cert in certsToCheck)
            {
                var violation = EvaluateCertificateAgainstRule(policy, rule, cert);
                if (violation != null)
                {
                    result.Violations.Add(violation);
                }
            }
        }

        private PolicyViolation? EvaluateCertificateAgainstRule(
            CertificateCompliancePolicy policy,
            PolicyRule rule,
            SecureBootCertificate cert)
        {
            switch (rule.RuleType)
            {
                case PolicyRuleType.MinimumKeySize:
                    if (int.TryParse(rule.Value, out int minKeySize))
                    {
                        if (cert.KeySize.HasValue && cert.KeySize.Value < minKeySize)
                        {
                            return CreateViolation(policy, rule, cert,
                                $"Certificate key size ({cert.KeySize} bits) is below minimum ({minKeySize} bits)");
                        }
                    }
                    break;

                case PolicyRuleType.AllowedSignatureAlgorithms:
                    if (!string.IsNullOrEmpty(rule.Value) && !string.IsNullOrEmpty(cert.SignatureAlgorithm))
                    {
                        var allowed = rule.Value.Split(',').Select(a => a.Trim()).ToList();
                        if (!allowed.Any(a => cert.SignatureAlgorithm.Contains(a, StringComparison.OrdinalIgnoreCase)))
                        {
                            return CreateViolation(policy, rule, cert,
                                $"Certificate signature algorithm '{cert.SignatureAlgorithm}' is not in allowed list: {rule.Value}");
                        }
                    }
                    break;

                case PolicyRuleType.DisallowedSignatureAlgorithms:
                    if (!string.IsNullOrEmpty(rule.Value) && !string.IsNullOrEmpty(cert.SignatureAlgorithm))
                    {
                        var disallowed = rule.Value.Split(',').Select(a => a.Trim()).ToList();
                        if (disallowed.Any(a => cert.SignatureAlgorithm.Contains(a, StringComparison.OrdinalIgnoreCase)))
                        {
                            return CreateViolation(policy, rule, cert,
                                $"Certificate signature algorithm '{cert.SignatureAlgorithm}' is disallowed");
                        }
                    }
                    break;

                case PolicyRuleType.MaximumCertificateAge:
                    if (int.TryParse(rule.Value, out int maxAgeDays) && cert.NotBefore.HasValue)
                    {
                        var age = (DateTimeOffset.UtcNow - cert.NotBefore.Value).Days;
                        if (age > maxAgeDays)
                        {
                            return CreateViolation(policy, rule, cert,
                                $"Certificate age ({age} days) exceeds maximum ({maxAgeDays} days)");
                        }
                    }
                    break;

                case PolicyRuleType.MinimumDaysUntilExpiration:
                    if (int.TryParse(rule.Value, out int minDays) && cert.DaysUntilExpiration.HasValue)
                    {
                        if (cert.DaysUntilExpiration.Value < minDays)
                        {
                            return CreateViolation(policy, rule, cert,
                                $"Certificate expires in {cert.DaysUntilExpiration} days, which is below threshold ({minDays} days)");
                        }
                    }
                    break;

                case PolicyRuleType.RequireMicrosoftCertificate:
                    if (!cert.IsMicrosoftCertificate)
                    {
                        return CreateViolation(policy, rule, cert,
                            "Certificate is not issued by Microsoft");
                    }
                    break;

                case PolicyRuleType.DisallowExpiredCertificates:
                    if (cert.IsExpired)
                    {
                        return CreateViolation(policy, rule, cert,
                            $"Certificate is expired (expired on {cert.NotAfter?.ToString("yyyy-MM-dd")})");
                    }
                    break;

                case PolicyRuleType.RequireSubjectPattern:
                    if (!string.IsNullOrEmpty(rule.Value) && !string.IsNullOrEmpty(cert.Subject))
                    {
                        try
                        {
                            if (!Regex.IsMatch(cert.Subject, rule.Value))
                            {
                                return CreateViolation(policy, rule, cert,
                                    $"Certificate subject '{cert.Subject}' does not match required pattern: {rule.Value}");
                            }
                        }
                        catch (ArgumentException ex)
                        {
                            // Invalid regex pattern - log and skip this rule
                            // Note: In production, consider logging this with a proper logger
                            System.Diagnostics.Debug.WriteLine($"Invalid regex pattern '{rule.Value}' in policy '{policy.Name}': {ex.Message}");
                        }
                    }
                    break;
            }

            return null;
        }

        private PolicyViolation CreateViolation(
            CertificateCompliancePolicy policy,
            PolicyRule rule,
            SecureBootCertificate cert,
            string message)
        {
            return new PolicyViolation
            {
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                Rule = rule,
                Message = message,
                CertificateThumbprint = cert.Thumbprint,
                Database = cert.Database
            };
        }
    }
}
