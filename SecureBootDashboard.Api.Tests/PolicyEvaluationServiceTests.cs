using System;
using System.Collections.Generic;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootDashboard.Api.Tests
{
    public class PolicyEvaluationServiceTests
    {
        [Fact]
        public void EvaluateCompliance_NoCertificates_ReturnsUnknown()
        {
            // Arrange
            var policies = new List<CertificateCompliancePolicy>();
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();

            // Act
            var result = service.EvaluateCompliance(deviceId, null, null, null);

            // Assert
            Assert.Equal(ComplianceStatus.Unknown, result.Status);
            Assert.Equal(deviceId, result.DeviceId);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void EvaluateCompliance_NoPolicies_ReturnsCompliant()
        {
            // Arrange
            var policies = new List<CertificateCompliancePolicy>();
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            var certificates = CreateSampleCertificates();

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.Compliant, result.Status);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void EvaluateCompliance_MinimumKeySize_DetectsViolation()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "Minimum Key Size Policy",
                IsEnabled = true,
                Priority = 1,
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.MinimumKeySize,
                        Severity = PolicySeverity.Critical,
                        Value = "2048"
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "ABC123",
                        Subject = "CN=Test Cert",
                        KeySize = 1024, // Below minimum
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.NonCompliant, result.Status);
            Assert.Single(result.Violations);
            Assert.Equal(policy.Id, result.Violations[0].PolicyId);
            Assert.Contains("1024", result.Violations[0].Message);
            Assert.Contains("2048", result.Violations[0].Message);
        }

        [Fact]
        public void EvaluateCompliance_ExpiredCertificate_DetectsViolation()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "No Expired Certs Policy",
                IsEnabled = true,
                Priority = 1,
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.DisallowExpiredCertificates,
                        Severity = PolicySeverity.Critical
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "DEF456",
                        Subject = "CN=Expired Cert",
                        IsExpired = true,
                        NotAfter = DateTimeOffset.UtcNow.AddDays(-10)
                    }
                }
            };

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.NonCompliant, result.Status);
            Assert.Single(result.Violations);
            Assert.Contains("expired", result.Violations[0].Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EvaluateCompliance_RequireMicrosoftCert_DetectsViolation()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "Microsoft Only Policy",
                IsEnabled = true,
                Priority = 1,
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.RequireMicrosoftCertificate,
                        Severity = PolicySeverity.Warning
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "GHI789",
                        Subject = "CN=Third Party Cert",
                        IsMicrosoftCertificate = false
                    }
                }
            };

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.Warning, result.Status);
            Assert.Single(result.Violations);
            Assert.Contains("Microsoft", result.Violations[0].Message);
        }

        [Fact]
        public void EvaluateCompliance_MinimumDaysUntilExpiration_DetectsViolation()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "Expiration Warning Policy",
                IsEnabled = true,
                Priority = 1,
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.MinimumDaysUntilExpiration,
                        Severity = PolicySeverity.Warning,
                        Value = "90"
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "JKL012",
                        Subject = "CN=Expiring Soon",
                        DaysUntilExpiration = 30, // Below 90 day threshold
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.Warning, result.Status);
            Assert.Single(result.Violations);
            Assert.Contains("30", result.Violations[0].Message);
            Assert.Contains("90", result.Violations[0].Message);
        }

        [Fact]
        public void EvaluateCompliance_DisabledPolicy_IsIgnored()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "Disabled Policy",
                IsEnabled = false, // Disabled
                Priority = 1,
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.DisallowExpiredCertificates,
                        Severity = PolicySeverity.Critical
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "MNO345",
                        IsExpired = true // Would violate if policy was enabled
                    }
                }
            };

            // Act
            var result = service.EvaluateCompliance(deviceId, null, certificates, null);

            // Assert
            Assert.Equal(ComplianceStatus.Compliant, result.Status);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void EvaluateCompliance_FleetScoped_OnlyAppliesToMatchingFleet()
        {
            // Arrange
            var policy = new CertificateCompliancePolicy
            {
                Id = Guid.NewGuid(),
                Name = "Fleet-Specific Policy",
                IsEnabled = true,
                Priority = 1,
                FleetId = "fleet-01",
                Rules = new List<PolicyRule>
                {
                    new PolicyRule
                    {
                        RuleType = PolicyRuleType.DisallowExpiredCertificates,
                        Severity = PolicySeverity.Critical
                    }
                }
            };

            var policies = new List<CertificateCompliancePolicy> { policy };
            var service = new PolicyEvaluationService(policies);
            var deviceId = Guid.NewGuid();
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "PQR678",
                        IsExpired = true
                    }
                }
            };

            // Act - device in different fleet
            var result = service.EvaluateCompliance(deviceId, null, certificates, "fleet-02");

            // Assert
            Assert.Equal(ComplianceStatus.Compliant, result.Status);
            Assert.Empty(result.Violations);
        }

        private SecureBootCertificateCollection CreateSampleCertificates()
        {
            return new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Database = "db",
                        Thumbprint = "123ABC",
                        Subject = "CN=Microsoft Corporation",
                        KeySize = 2048,
                        IsExpired = false,
                        IsMicrosoftCertificate = true,
                        DaysUntilExpiration = 365
                    }
                }
            };
        }
    }
}
