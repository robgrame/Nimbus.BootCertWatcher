using System;
using SecureBootDashboard.Api.Controllers;
using Xunit;
using static SecureBootDashboard.Api.Controllers.DevicesController;

namespace SecureBootDashboard.Api.Tests.Controllers
{
    /// <summary>
    /// Tests for DeviceSummaryResponse ReadyToUpdate logic
    /// </summary>
    public class DeviceSummaryResponseTests
    {
        [Fact]
        public void ReadyToUpdate_WhenBothFirmwareAndOSReady_ReturnsTrue()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 6, 15),
                osBuildNumber: "26100" // Windows 11 24H2
            );

            // Act & Assert
            Assert.True(response.ReadyToUpdate);
            Assert.True(response.IsFirmwareReady);
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void ReadyToUpdate_WhenFirmwareNotReady_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2023, 12, 31), // Before 2024
                osBuildNumber: "26100" // OS is ready
            );

            // Act & Assert
            Assert.False(response.ReadyToUpdate);
            Assert.False(response.IsFirmwareReady);
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void ReadyToUpdate_WhenOSNotReady_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 3, 1), // Firmware is ready
                osBuildNumber: "19045" // Old Windows 10 build
            );

            // Act & Assert
            Assert.False(response.ReadyToUpdate);
            Assert.True(response.IsFirmwareReady);
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void ReadyToUpdate_WhenNeitherReady_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2023, 1, 1),
                osBuildNumber: "19041" // Old build
            );

            // Act & Assert
            Assert.False(response.ReadyToUpdate);
            Assert.False(response.IsFirmwareReady);
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsFirmwareReady_OnJanuaryFirst2024_ReturnsTrue()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "26100"
            );

            // Act & Assert
            Assert.True(response.IsFirmwareReady);
        }

        [Fact]
        public void IsFirmwareReady_BeforeJanuaryFirst2024_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2023, 12, 31),
                osBuildNumber: "26100"
            );

            // Act & Assert
            Assert.False(response.IsFirmwareReady);
        }

        [Fact]
        public void IsFirmwareReady_WhenNull_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: null,
                osBuildNumber: "26100"
            );

            // Act & Assert
            Assert.False(response.IsFirmwareReady);
        }

        [Fact]
        public void IsOSUpdateReady_Windows11_24H2_Build26100_ReturnsTrue()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "26100"
            );

            // Act & Assert
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_Windows11_24H2_Build26101_ReturnsTrue()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "26101"
            );

            // Act & Assert
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_Windows10_22H2_Build19046_ReturnsTrue()
        {
            // Arrange - Build 19046 or higher
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "19046"
            );

            // Act & Assert
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_Windows10_22H2_Build19045_ReturnsFalse()
        {
            // Arrange - Old build
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "19045"
            );

            // Act & Assert
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_WindowsServer2022_Build20349_ReturnsTrue()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "20349"
            );

            // Act & Assert
            Assert.True(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_WindowsServer2022_Build20348_ReturnsFalse()
        {
            // Arrange - Old build
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "20348"
            );

            // Act & Assert
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_WhenBuildNumberNull_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: null
            );

            // Act & Assert
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_WhenBuildNumberEmpty_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: ""
            );

            // Act & Assert
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void IsOSUpdateReady_WhenBuildNumberInvalid_ReturnsFalse()
        {
            // Arrange
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 1, 1),
                osBuildNumber: "invalid"
            );

            // Act & Assert
            Assert.False(response.IsOSUpdateReady);
        }

        [Fact]
        public void ReadyToUpdate_RealWorldScenario_Windows11_24H2_WithNewFirmware_ReturnsTrue()
        {
            // Arrange - Typical scenario: new device with Windows 11 24H2 and 2024 firmware
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2024, 8, 15),
                osBuildNumber: "26100"
            );

            // Act & Assert
            Assert.True(response.ReadyToUpdate);
        }

        [Fact]
        public void ReadyToUpdate_RealWorldScenario_OldDevice_ReturnsFalse()
        {
            // Arrange - Typical scenario: old device that needs updates
            var response = CreateDeviceSummary(
                firmwareReleaseDate: new DateTime(2022, 5, 1),
                osBuildNumber: "19044" // Windows 10 21H2
            );

            // Act & Assert
            Assert.False(response.ReadyToUpdate);
        }

        /// <summary>
        /// Helper method to create a DeviceSummaryResponse with minimal required fields
        /// </summary>
        private DeviceSummaryResponse CreateDeviceSummary(
            DateTime? firmwareReleaseDate,
            string? osBuildNumber)
        {
            return new DeviceSummaryResponse(
                Id: Guid.NewGuid(),
                MachineName: "TEST-PC",
                DomainName: "test.local",
                FleetId: "fleet-01",
                Manufacturer: "Test Manufacturer",
                Model: "Test Model",
                FirstSeenUtc: DateTimeOffset.UtcNow.AddDays(-30),
                LastSeenUtc: DateTimeOffset.UtcNow,
                ReportCount: 10,
                LatestDeploymentState: "Updated",
                LatestReportDate: DateTimeOffset.UtcNow,
                UEFISecureBootEnabled: true,
                ClientVersion: "1.0.0",
                OperatingSystem: "Microsoft Windows 11 Pro",
                OSVersion: "10.0." + (osBuildNumber ?? "0"),
                OSBuildNumber: osBuildNumber,
                OSProductType: 1,
                ChassisTypesJson: null,
                IsVirtualMachine: false,
                VirtualizationPlatform: null,
                FirmwareReleaseDate: firmwareReleaseDate,
                AllowTelemetry: 1,
                MicrosoftUpdateManagedOptIn: true,
                WindowsUEFICA2023Capable: 1
            );
        }
    }
}
