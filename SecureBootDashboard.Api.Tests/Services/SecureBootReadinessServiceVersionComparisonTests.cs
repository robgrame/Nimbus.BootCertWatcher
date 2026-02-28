using System;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Tests.Services
{
    public class SecureBootReadinessServiceVersionComparisonTests
    {
        private readonly ISecureBootReadinessService _service;
        private readonly SecureBootReadinessOptions _options;

        public SecureBootReadinessServiceVersionComparisonTests()
        {
            _options = new SecureBootReadinessOptions
            {
                MinimumOSBuildVersions = new Dictionary<string, string>
                {
                    { "Windows11_25H2", "10.0.26200.7171" },
                    { "Windows11_24H2", "10.0.26100.7171" },
                    { "Windows11_23H2", "10.0.22631.6139" },
                    { "Windows11_22H2", "10.0.22621.6060" },
                    { "Windows10_22H2", "10.0.19045.6456" }
                }
            };

            var optionsMonitor = Mock.Of<IOptions<SecureBootReadinessOptions>>(o => o.Value == _options);
            var logger = Mock.Of<ILogger<SecureBootReadinessService>>();

            _service = new SecureBootReadinessService(optionsMonitor, logger);
        }

        [Theory]
        [InlineData("10.0.26200.7171", "10.0.26200.7171", true)]  // Exact match
        [InlineData("10.0.26200.7172", "10.0.26200.7171", true)]  // Higher revision
        [InlineData("10.0.26200.7170", "10.0.26200.7171", false)] // Lower revision
        [InlineData("10.0.26200", "10.0.26200.7171", false)]      // Missing revision (treated as .0)
        [InlineData("10.0.26200.8000", "10.0.26200.7171", true)]  // Much higher revision
        [InlineData("10.0.26200.0", "10.0.26200.7171", false)]    // Explicit .0 revision
        public void EvaluateReadiness_OSVersion_4PartComparison(string current, string required, bool expectedReady)
        {
            // Arrange
            _options.MinimumOSBuildVersions["Windows11_25H2"] = required;

            // Act
            var result = _service.EvaluateReadiness(null, current, null);

            // Assert
            Assert.Equal(expectedReady, result.IsOSReady);
        }

        [Fact]
        public void EvaluateReadiness_Windows11_25H2_ExactVersion_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.26200.7171";

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.True(result.IsOSReady);
            Assert.Contains("meets requirements", result.OSEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_Windows11_25H2_HigherRevision_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.26200.8000";

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.True(result.IsOSReady);
            Assert.Contains("meets requirements", result.OSEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_Windows11_25H2_LowerRevision_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.26200.7000";

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.False(result.IsOSReady);
            Assert.Contains("does not meet requirements", result.OSEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_Windows11_25H2_MissingRevision_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.26200"; // No revision = treated as .0

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.False(result.IsOSReady);
            // The message may indicate incomplete version or not meeting requirements
            Assert.True(
                result.OSEvaluationDetails.Contains("incomplete") || 
                result.OSEvaluationDetails.Contains("does not meet requirements"),
                $"Expected message about incomplete version or requirements, got: {result.OSEvaluationDetails}");
        }

        [Fact]
        public void EvaluateReadiness_Windows11_24H2_CompleteVersion_ShouldBeReady()
        {
            // Arrange
            var osVersion = "10.0.26100.7171";

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.True(result.IsOSReady);
        }

        [Fact]
        public void EvaluateReadiness_Windows10_22H2_FourParts_ShouldBeEvaluated()
        {
            // Arrange
            var osVersion = "10.0.19045.6456";

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.True(result.IsOSReady);
            Assert.Contains("10.0.19045.6456 meets requirements", result.OSEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_Windows10_22H2_ThreeParts_ShouldFail()
        {
            // Arrange
            var osVersion = "10.0.19045"; // Missing revision

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.False(result.IsOSReady);
            // Check for "does not meet requirements" or "incomplete" (incomplete for 3-part versions)
            Assert.True(
                result.OSEvaluationDetails.Contains("does not meet requirements") ||
                result.OSEvaluationDetails.Contains("incomplete"),
                $"Expected failure message, got: {result.OSEvaluationDetails}");
        }

        [Theory]
        [InlineData("10.0.26200.7171")]
        [InlineData("10.0.26200.8000")]
        [InlineData("10.0.26200.9999")]
        public void EvaluateReadiness_VersionDetails_ShouldShowCorrectMessage_WhenReady(string osVersion)
        {
            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.Contains("meets requirements", result.OSEvaluationDetails);
            Assert.Contains(">= 10.0.26200.7171", result.OSEvaluationDetails);
        }

        [Theory]
        [InlineData("10.0.26200.7170")]
        [InlineData("10.0.26200.0")]
        [InlineData("10.0.26200")]
        [InlineData("10.0.26200.1000")]
        public void EvaluateReadiness_VersionDetails_ShouldShowCorrectMessage_WhenNotReady(string osVersion)
        {
            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            // Check for "does not meet requirements" or "incomplete" (incomplete for 3-part versions)
            Assert.True(
                result.OSEvaluationDetails.Contains("does not meet requirements") ||
                result.OSEvaluationDetails.Contains("incomplete"),
                $"Expected failure message, got: {result.OSEvaluationDetails}");
        }

        [Theory]
        [InlineData("10.0.26201.0")]     // Different build, should use Windows11_25H2 requirement
        [InlineData("10.0.26300.5000")]  // Future build, should use Windows11_25H2 requirement
        [InlineData("10.0.27000.1000")]  // Future major build, should use Windows11_25H2 requirement
        public void EvaluateReadiness_FutureBuilds_ShouldUseLatestRequirement(string osVersion)
        {
            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            // Future builds should be compared against Windows11_25H2 requirement
            Assert.True(result.IsOSReady);
            Assert.Contains("meets requirements", result.OSEvaluationDetails);
        }

        [Theory]
        [InlineData("10.0.26100.7171")]  // Windows 11 24H2 minimum
        [InlineData("10.0.26100.8000")]  // Windows 11 24H2 higher
        [InlineData("10.0.26199.9999")]  // Just below 26200, should use 24H2 requirement
        public void EvaluateReadiness_Windows11_24H2_Range_ShouldWork(string osVersion)
        {
            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.True(result.IsOSReady);
            Assert.Contains("meets requirements", result.OSEvaluationDetails);
        }

        [Fact]
        public void EvaluateReadiness_BelowMinimum24H2_ShouldNotBeReady()
        {
            // Arrange
            var osVersion = "10.0.26100.7000"; // Below 24H2 minimum

            // Act
            var result = _service.EvaluateReadiness(null, osVersion, null);

            // Assert
            Assert.False(result.IsOSReady);
            Assert.Contains("does not meet requirements", result.OSEvaluationDetails);
        }
    }
}
