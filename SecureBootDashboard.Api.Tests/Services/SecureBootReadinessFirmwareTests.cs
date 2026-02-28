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
    /// Tests for SecureBootReadinessService focusing on firmware confidence criteria.
    /// Tests that readiness now considers firmware release date in addition to certificates and OS.
    /// </summary>
    public class SecureBootReadinessFirmwareTests
    {
        private readonly ISecureBootReadinessService _service;
        private readonly SecureBootReadinessOptions _options;

        public SecureBootReadinessFirmwareTests()
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
                }
            };
        }

        #region Firmware Confidence Level Tests

        [Fact]
        public void EvaluateReadiness_FirmwareAfterJan2025_ShouldHaveHighConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2025, 2, 15); // Feb 2025

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.High, result.FirmwareConfidence);
            Assert.Contains("High confidence", result.FirmwareEvaluationDetails);
            Assert.Contains("2025-02-15", result.FirmwareEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareExactlyJan12025_ShouldHaveHighConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2025, 1, 1); // Exactly Jan 1, 2025

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.High, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareIn2024_ShouldHaveMediumConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2024, 6, 15); // June 2024

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Medium, result.FirmwareConfidence);
            Assert.Contains("Medium confidence", result.FirmwareEvaluationDetails);
            Assert.Contains("2024-06-15", result.FirmwareEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareEarly2024_ShouldHaveMediumConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2024, 1, 1); // Jan 1, 2024

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Medium, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareDec312024_ShouldHaveMediumConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2024, 12, 31); // Last day of 2024

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Medium, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareBefore2024_ShouldHaveLowConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2023, 11, 15); // Nov 2023

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Low, result.FirmwareConfidence);
            Assert.Contains("Low confidence", result.FirmwareEvaluationDetails);
            Assert.Contains("2023-11-15", result.FirmwareEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareVeryOld_ShouldHaveLowConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2020, 5, 1); // 2020

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Low, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_FirmwareDec312023_ShouldHaveLowConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2023, 12, 31); // Last day before 2024

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Low, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_NoFirmwareDate_ShouldHaveUnknownConfidence()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, null);

            // Assert
            Assert.Equal(FirmwareConfidenceLevel.Unknown, result.FirmwareConfidence);
            Assert.Contains("not available", result.FirmwareEvaluationDetails);
        }

        #endregion

        #region Readiness Impact Tests

        [Fact]
        public void EvaluateReadiness_HighConfidenceFirmware_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2025, 2, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device with HIGH firmware confidence should be ready");
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
        }

        [Fact]
        public void EvaluateReadiness_MediumConfidenceFirmware_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2024, 6, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device with MEDIUM firmware confidence should still be ready");
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
        }

        [Fact]
        public void EvaluateReadiness_LowConfidenceFirmware_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2023, 6, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate, "Device with LOW firmware confidence should NOT be ready");
            Assert.True(result.IsOSReady); // OS is still valid
            Assert.True(result.AreOemCertificatesValid); // Certs are still valid
            Assert.Equal(FirmwareConfidenceLevel.Low, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_UnknownConfidenceFirmware_ShouldBeReady()
        {
            // Arrange - Unknown firmware date should not block readiness (we allow with warning)
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, null);

            // Assert
            Assert.True(result.IsReadyToUpdate, "Device with UNKNOWN firmware confidence should still be ready (with warning)");
            Assert.Equal(FirmwareConfidenceLevel.Unknown, result.FirmwareConfidence);
        }

        #endregion

        #region Combined Criteria Tests

        [Fact]
        public void EvaluateReadiness_LowFirmware_ExpiredCerts_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = new SecureBootCertificateCollection
            {
                SignatureDatabase = new List<SecureBootCertificate>
                {
                    new SecureBootCertificate
                    {
                        Subject = "CN=OEM Test Certificate",
                        Issuer = "CN=OEM CA",
                        NotBefore = DateTimeOffset.UtcNow.AddYears(-3),
                        NotAfter = DateTimeOffset.UtcNow.AddDays(-1),
                        IsExpired = true,
                        IsMicrosoftCertificate = false,
                        Thumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD"
                    }
                }
            };
            var firmwareDate = new DateTime(2023, 6, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate);
            Assert.False(result.AreOemCertificatesValid);
            Assert.Equal(FirmwareConfidenceLevel.Low, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_HighFirmware_BadOS_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.100"; // Too old
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2025, 2, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.False(result.IsReadyToUpdate);
            Assert.False(result.IsOSReady);
            Assert.Equal(FirmwareConfidenceLevel.High, result.FirmwareConfidence);
        }

        [Fact]
        public void EvaluateReadiness_AllCriteriaPass_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = new DateTime(2025, 3, 15);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.True(result.IsReadyToUpdate);
            Assert.True(result.IsOSReady);
            Assert.True(result.AreOemCertificatesValid);
            Assert.Equal(FirmwareConfidenceLevel.High, result.FirmwareConfidence);
        }

        #endregion

        #region Boundary Tests

        [Theory]
        [InlineData("2023-12-31", FirmwareConfidenceLevel.Low)]      // Last day before 2024
        [InlineData("2024-01-01", FirmwareConfidenceLevel.Medium)]   // First day of 2024
        [InlineData("2024-12-31", FirmwareConfidenceLevel.Medium)]   // Last day of 2024
        [InlineData("2025-01-01", FirmwareConfidenceLevel.High)]     // First day of 2025
        public void EvaluateReadiness_BoundaryDates_ShouldHaveCorrectConfidence(
            string dateString, 
            FirmwareConfidenceLevel expectedLevel)
        {
            // Arrange
            var osVersion = "10.0.22621.6060";
            var certificates = CreateValidCertificates();
            var firmwareDate = DateTime.Parse(dateString);

            // Act
            var result = _service.EvaluateReadiness(certificates, osVersion, null, firmwareDate);

            // Assert
            Assert.Equal(expectedLevel, result.FirmwareConfidence);
        }

        #endregion
    }
}
