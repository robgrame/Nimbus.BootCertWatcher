using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Tests.Services
{
    /// <summary>
    /// Tests for SecureBootReadinessService focusing on certificate-based readiness criteria.
    /// Tests that readiness is based on OEM certificate expiration, not Windows CA 2023 presence.
    /// </summary>
    public class SecureBootReadinessCertificateTests
    {
        private readonly ISecureBootReadinessService _service;
        private readonly SecureBootReadinessOptions _options;

        public SecureBootReadinessCertificateTests()
        {
            _options = new SecureBootReadinessOptions
            {
                CertificateExpirationWarningDays = 180,
                CertificateExpirationCriticalDays = 90,
                RequireWindowsUEFICA2023 = false, // Not required for readiness (gets installed during upgrade)
                WindowsUEFICA2023Thumbprint = "45A0FA32604773C82433C3B7D59E7466B3AC0C67",
                RequireOemCertificatesValid = true,
                MinimumOSBuildVersions = new Dictionary<string, string>
                {
                    { "Windows11_22H2", "10.0.22621.6060" },
                    { "Windows10_22H2", "10.0.19045.6456" }
                }
            };

            var optionsMonitor = Mock.Of<IOptions<SecureBootReadinessOptions>>(o => o.Value == _options);
            var logger = Mock.Of<ILogger<SecureBootReadinessService>>();

            _service = new SecureBootReadinessService(optionsMonitor, logger);
        }

        [Fact]
        public void EvaluateReadiness_ValidOemCerts_NoWindowsCA2023_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Valid OEM certificate (not expired, not expiring soon)
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready when OS and OEM certs are valid, even without Windows CA 2023");
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
            Assert.False(result.HasWindowsUEFICA2023); // Not present but should not block readiness
            Assert.Equal(1, result.ValidOemCertificateCount);
            Assert.Equal(0, result.ExpiredOemCertificateCount);
            Assert.Equal(0, result.CriticalOemCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_ExpiredOemCerts_WithWindowsCA2023_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Expired OEM certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-3),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(-1), // Expired yesterday
                        IsExpired = true,
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    },
                    // Windows CA 2023 certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=Windows UEFI CA 2023",
                        Issuer = "CN=Microsoft Corporation Third Party Marketplace Root",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-1),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(10),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "45A0FA32604773C82433C3B7D59E7466B3AC0C67"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when OEM certs are expired, even with Windows CA 2023");
            Assert.True(result.IsOSReady);
            Assert.False(result.AreOemCertificatesValid); // OEM cert expired
            Assert.True(result.HasWindowsUEFICA2023); // Present but should not make device ready
            Assert.Equal(1, result.ExpiredOemCertificateCount);
            Assert.Equal(0, result.ValidOemCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_CriticalExpiringOemCerts_NoWindowsCA2023_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // OEM certificate expiring in 30 days (within critical threshold of 90 days)
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(30),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when OEM certs are expiring soon");
            Assert.True(result.IsOSReady);
            Assert.False(result.AreOemCertificatesValid); // Expiring critically soon
            Assert.False(result.HasWindowsUEFICA2023);
            Assert.Equal(0, result.ExpiredOemCertificateCount);
            Assert.Equal(1, result.CriticalOemCertificateCount);
            Assert.Equal(0, result.ValidOemCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_WarningExpiringOemCerts_NoWindowsCA2023_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // OEM certificate expiring in 120 days (within warning threshold of 180 days, but outside critical 90 days)
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(120),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready when OEM certs are only in warning period (not critical)");
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid); // Warning but not critical
            Assert.False(result.HasWindowsUEFICA2023);
            Assert.Equal(0, result.ExpiredOemCertificateCount);
            Assert.Equal(0, result.CriticalOemCertificateCount);
            Assert.Equal(1, result.WarningOemCertificateCount);
            Assert.Equal(0, result.ValidOemCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_MixedOemCerts_ValidAndWarning_NoWindowsCA2023_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Valid OEM certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate 1",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    },
                    // Warning OEM certificate (expiring in 120 days)
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate 2",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(120),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "112233445566778899AABBCCDDEEFF0011223344"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready with mix of valid and warning OEM certs");
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
            Assert.Equal(0, result.ExpiredOemCertificateCount);
            Assert.Equal(0, result.CriticalOemCertificateCount);
            Assert.Equal(1, result.WarningOemCertificateCount);
            Assert.Equal(1, result.ValidOemCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_NoOemCerts_NoWindowsCA2023_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Only Microsoft certificates (no OEM)
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Windows Production PCA 2011",
                        Issuer = "CN=Microsoft Root Certificate Authority 2010",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-5),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(5),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "580A6F4CC4E4B669B9EBDC1B2B3E087B80D0678D"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when no OEM certificates are found");
            Assert.True(result.IsOSReady);
            Assert.False(result.AreOemCertificatesValid); // No OEM certs found
            Assert.True(result.HasNoOemCertificates);
            Assert.Contains("No OEM certificates found", result.CertificateEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_InvalidOS_ValidOemCerts_NoWindowsCA2023_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.19045.1000"; // Below minimum
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Valid OEM certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when OS version is below minimum");
            Assert.False(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
            Assert.False(result.HasWindowsUEFICA2023);
        }

        [Fact]
        public void EvaluateReadiness_CertificateEvaluationDetails_ShouldIndicateWindowsCA2023IsInformational()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Valid OEM certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.Contains("ℹ️ Windows UEFI CA 2023 not yet installed", result.CertificateEvaluationDetails);
            Assert.DoesNotContain("❌ Windows UEFI CA 2023", result.CertificateEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_WithWindowsCA2023Present_ShouldIndicateInDetails()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    // Valid OEM certificate
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    },
                    // Windows CA 2023
                    new SecureBootCertificate
                    {
                        Subject = "CN=Windows UEFI CA 2023",
                        Issuer = "CN=Microsoft Corporation Third Party Marketplace Root",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-1),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(10),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "45A0FA32604773C82433C3B7D59E7466B3AC0C67"
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null);

            // Assert
            Assert.Contains("✅ Windows UEFI CA 2023 already present", result.CertificateEvaluationDetails);
            Assert.True(result.HasWindowsUEFICA2023);
        }
    }
}
