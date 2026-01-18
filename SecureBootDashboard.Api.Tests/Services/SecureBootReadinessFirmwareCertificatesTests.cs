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
    /// Tests for SecureBootReadinessService focusing on Platform Key (PK) validation.
    /// Tests that readiness is blocked when Platform Keys (PK) are expired.
    /// Note: KEK (Key Exchange Keys) and DB (Signature Database) certificates are not evaluated
    /// because they will be updated during the Secure Boot upgrade process.
    /// </summary>
    public class SecureBootReadinessFirmwareCertificatesTests
    {
        private readonly ISecureBootReadinessService _service;
        private readonly SecureBootReadinessOptions _options;

        public SecureBootReadinessFirmwareCertificatesTests()
        {
            _options = new SecureBootReadinessOptions
            {
                CertificateExpirationWarningDays = 180,
                CertificateExpirationCriticalDays = 90,
                RequireWindowsUEFICA2023 = false,
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

        private SecureBootCertificateCollection CreateValidCertificates()
        {
            return new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                },
                KeyExchangeKeys = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Corporation KEK CA 2011",
                        Issuer = "CN=Microsoft Corporation Third Party Marketplace Root",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "31590BFD89C9D74ED087DFAC66334B3931254B30",
                        IsExpired = false
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Corporation KEK 2K CA 2023",
                        Issuer = "CN=Microsoft RSA Devices Root CA 2021",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "459AB6FB5E284D272D5E3E6ABC8ED663829D632B",
                        IsExpired = false
                    }
                }
            };
        }

        #region Platform Key (PK) Expiration Tests

        [Fact]
        public void EvaluateReadiness_WithExpiredPlatformKey_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1); // Recent firmware
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Hyper-V Firmware PK",
                        Issuer = "CN=Microsoft Corporation Third Party Marketplace PCA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-10),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(-4281), // Expired long ago
                        IsMicrosoftCertificate = true,
                        Thumbprint = "8058E8CC51749652804BBD6F39AED713D119C64B",
                        IsExpired = true
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when Platform Key is expired");
            Assert.False(result.AreFirmwareCertificatesValid, "Firmware certificates should be marked invalid");
            Assert.Equal(1, result.ExpiredPlatformKeyCertificateCount);
            Assert.Contains("expired", result.CertificateEvaluationDetails, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EvaluateReadiness_WithCriticalPlatformKey_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1);
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft KEK CA",
                        Issuer = "CN=Microsoft Root",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(30), // Expires in 30 days (within critical threshold of 90)
                        IsMicrosoftCertificate = true,
                        Thumbprint = "CCCCDDDDEEEEFFFFGGGGHHHH",
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready when Platform Key is expiring soon");
            Assert.False(result.AreFirmwareCertificatesValid, "Firmware certificates should be marked invalid");
            Assert.Equal(1, result.CriticalPlatformKeyCertificateCount);
        }

        #endregion

        #region KEK/DB Expiration (Should NOT block readiness) Tests

        [Fact]
        public void EvaluateReadiness_WithExpiringKEK_ShouldStillBeReady()
        {
            // Arrange - KEK that will be updated during Secure Boot upgrade
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1);
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    },
                    // New KEK that will replace the old one
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Corporation KEK 2K CA 2023",
                        Issuer = "CN=Microsoft RSA Devices Root CA 2021",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(10),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "459AB6FB5E284D272D5E3E6ABC8ED663829D632B",
                        IsExpired = false
                    }
                },
                KeyExchangeKeys = new List<SecureBootCertificate>
                {
                    // Old KEK expiring soon - will be replaced during upgrade
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Corporation KEK CA 2011",
                        Issuer = "CN=Microsoft Third Party Marketplace Root",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-2),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(60), // Expires soon but will be replaced
                        IsMicrosoftCertificate = true,
                        Thumbprint = "31590BFD89C9D74ED087DFAC66334B3931254B30",
                        IsExpired = false
                    },
                    // New KEK already present
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Corporation KEK 2K CA 2023",
                        Issuer = "CN=Microsoft RSA Devices Root CA 2021",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(10),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "459AB6FB5E284D272D5E3E6ABC8ED663829D632B",
                        IsExpired = false
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    // Valid PK
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Valid PK",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(5),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready even with expiring KEK that will be updated during upgrade");
            Assert.True(result.AreFirmwareCertificatesValid, "PK is valid, so firmware certificates are valid");
        }

        [Fact]
        public void EvaluateReadiness_WithExpiringLegacyDB_ShouldStillBeReady()
        {
            // Arrange - DB certificate expiring in 2026 that will be updated during Secure Boot upgrade
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1);
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    },
                    // Old DB cert expiring 2026 - will be replaced
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Windows Production PCA 2011",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "580A6F4CC4E4B669B9EBDC1B2B3E087B80D0678D",
                        IsExpired = false
                    },
                    // New DB cert that will replace it
                    new SecureBootCertificate
                    {
                        Subject = "CN=Windows UEFI CA 2023",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(10),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "45A0FA32604773C82433C3B7D59E7466B3AC0C67",
                        IsExpired = false
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    // Valid PK
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft Valid PK",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(5),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "BBBBBBBBBBBBBBBBBBBBBBBBBBBB",
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready even with legacy DB cert expiring 2026 - will be updated during upgrade");
            Assert.True(result.AreFirmwareCertificatesValid, "PK is valid, so firmware certificates are valid");
        }

        #endregion

        #region Valid Firmware Certificates Tests

        [Fact]
        public void EvaluateReadiness_WithValidPlatformKey_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1);
            var certificates = CreateValidCertificates();

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device should be ready with all valid certificates");
            Assert.True(result.AreFirmwareCertificatesValid, "Firmware certificates should be valid");
            Assert.Equal(0, result.ExpiredPlatformKeyCertificateCount);
            Assert.Equal(0, result.CriticalPlatformKeyCertificateCount);
        }

        [Fact]
        public void EvaluateReadiness_WithMultipleExpiredPlatformKeys_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var firmwareDate = new DateTime(2025, 6, 1);
            
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotAfter = DateTimeOffset.UtcNow.AddYears(2),
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                },
                PlatformKeys = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft PK 1",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = DateTimeOffset.UtcNow.AddDays(-100),
                        IsMicrosoftCertificate = true,
                        Thumbprint = "11111111111111111111111111111111",
                        IsExpired = true
                    },
                    new SecureBootCertificate
                    {
                        Subject = "CN=Microsoft PK 2",
                        Issuer = "CN=Microsoft Root",
                        NotAfter = DateTimeOffset.UtcNow.AddDays(30), // Critical
                        IsMicrosoftCertificate = true,
                        Thumbprint = "22222222222222222222222222222222",
                        IsExpired = false
                    }
                }
            };

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device should NOT be ready with multiple expired/critical PK");
            Assert.False(result.AreFirmwareCertificatesValid);
            Assert.Equal(1, result.ExpiredPlatformKeyCertificateCount);
            Assert.Equal(1, result.CriticalPlatformKeyCertificateCount);
        }

        #endregion
    }
}
