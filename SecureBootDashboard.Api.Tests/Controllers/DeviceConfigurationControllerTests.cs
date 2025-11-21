using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SecureBootDashboard.Api.Controllers;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Controllers
{
    public class DeviceConfigurationControllerTests : IDisposable
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly DeviceConfigurationController _controller;
        private readonly Mock<ILogger<DeviceConfigurationController>> _mockLogger;

        public DeviceConfigurationControllerTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<SecureBootDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new SecureBootDbContext(options);
            _mockLogger = new Mock<ILogger<DeviceConfigurationController>>();
            _controller = new DeviceConfigurationController(_dbContext, _mockLogger.Object);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }

        [Fact]
        public async Task CommandCertificateUpdate_DeviceNotFound_ReturnsNotFound()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var command = new CertificateUpdateCommand
            {
                UpdateType = 1,
                ForceUpdate = false
            };

            // Act
            var result = await _controller.CommandCertificateUpdateAsync(deviceId, command, default);

            // Assert
            Assert.NotNull(result);
            var notFoundResult = Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(result.Result);
            Assert.NotNull(notFoundResult.Value);
        }

        [Fact]
        public async Task CommandCertificateUpdate_DeviceWithoutSecureBoot_ReturnsBadRequest()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                UEFISecureBootEnabled = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);
            await _dbContext.SaveChangesAsync();

            var command = new CertificateUpdateCommand
            {
                UpdateType = 1,
                ForceUpdate = false
            };

            // Act
            var result = await _controller.CommandCertificateUpdateAsync(deviceId, command, default);

            // Assert
            Assert.NotNull(result);
            var badRequestResult = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
            var resultValue = badRequestResult.Value as DeviceConfigurationResult;
            Assert.NotNull(resultValue);
            Assert.False(resultValue.Success);
            Assert.Contains("UEFI Secure Boot", resultValue.Message);
        }

        [Fact]
        public async Task CommandCertificateUpdate_ValidDevice_ReturnsSuccess()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                UEFISecureBootEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            // Add a report with registry state
            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 1, optIn: true);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            var command = new CertificateUpdateCommand
            {
                UpdateType = 1,
                ForceUpdate = false
            };

            // Act
            var result = await _controller.CommandCertificateUpdateAsync(deviceId, command, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var resultValue = okResult.Value as DeviceConfigurationResult;
            Assert.NotNull(resultValue);
            Assert.True(resultValue.Success);
            Assert.Contains("queued successfully", resultValue.Message);
            Assert.Equal(command.CommandId, resultValue.CommandId);
            Assert.Equal(deviceId, resultValue.DeviceId);
        }

        [Fact]
        public async Task ConfigureMicrosoftUpdateOptIn_NoDevicesFound_ReturnsBadRequest()
        {
            // Arrange
            var command = new MicrosoftUpdateOptInCommand
            {
                OptIn = true
            };
            var deviceIds = new List<Guid> { Guid.NewGuid() };

            // Act
            var result = await _controller.ConfigureMicrosoftUpdateOptInAsync(command, deviceIds, null, default);

            // Assert
            Assert.NotNull(result);
            var badRequestResult = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task ConfigureMicrosoftUpdateOptIn_MultipleDevices_ReturnsSuccessForAll()
        {
            // Arrange
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            
            var device1 = new DeviceEntity
            {
                Id = device1Id,
                MachineName = "TEST-PC-1",
                FleetId = "fleet1",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            var device2 = new DeviceEntity
            {
                Id = device2Id,
                MachineName = "TEST-PC-2",
                FleetId = "fleet1",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            
            _dbContext.Devices.AddRange(device1, device2);
            await _dbContext.SaveChangesAsync();

            var command = new MicrosoftUpdateOptInCommand
            {
                OptIn = true
            };

            // Act
            var result = await _controller.ConfigureMicrosoftUpdateOptInAsync(
                command, null, "fleet1", default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var batchResult = okResult.Value as DeviceConfigurationController.DeviceConfigurationBatchResult;
            Assert.NotNull(batchResult);
            Assert.Equal(2, batchResult.TotalDevices);
            Assert.Equal(2, batchResult.SuccessCount);
            Assert.Equal(0, batchResult.FailureCount);
        }

        [Fact]
        public async Task ConfigureTelemetryLevel_InvalidLevel_ReturnsBadRequest()
        {
            // Arrange
            var command = new TelemetryConfigurationCommand
            {
                RequiredTelemetryLevel = 99, // Invalid level
                ValidateOnly = true
            };

            // Act
            var result = await _controller.ConfigureTelemetryLevelAsync(command, null, null, default);

            // Assert
            Assert.NotNull(result);
            var badRequestResult = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task ConfigureTelemetryLevel_ValidateOnly_ChecksCurrentLevel()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            // Add report with telemetry level = 2 (Enhanced)
            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 2, optIn: true);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            var command = new TelemetryConfigurationCommand
            {
                RequiredTelemetryLevel = 1, // Basic
                ValidateOnly = true
            };
            var deviceIds = new List<Guid> { deviceId };

            // Act
            var result = await _controller.ConfigureTelemetryLevelAsync(command, deviceIds, null, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var batchResult = okResult.Value as DeviceConfigurationController.DeviceConfigurationBatchResult;
            Assert.NotNull(batchResult);
            Assert.Equal(1, batchResult.TotalDevices);
            Assert.Equal(1, batchResult.SuccessCount); // Should pass validation
            Assert.Equal(0, batchResult.FailureCount);

            var deviceResult = batchResult.Results.First();
            Assert.True(deviceResult.Success);
            Assert.Contains("validation passed", deviceResult.Message);
        }

        [Fact]
        public async Task ConfigureTelemetryLevel_ValidateOnly_FailsWhenBelowRequired()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            // Add report with telemetry level = 0 (Security)
            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 0, optIn: true);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            var command = new TelemetryConfigurationCommand
            {
                RequiredTelemetryLevel = 1, // Basic
                ValidateOnly = true
            };
            var deviceIds = new List<Guid> { deviceId };

            // Act
            var result = await _controller.ConfigureTelemetryLevelAsync(command, deviceIds, null, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var batchResult = okResult.Value as DeviceConfigurationController.DeviceConfigurationBatchResult;
            Assert.NotNull(batchResult);
            Assert.Equal(1, batchResult.TotalDevices);
            Assert.Equal(0, batchResult.SuccessCount);
            Assert.Equal(1, batchResult.FailureCount); // Should fail validation

            var deviceResult = batchResult.Results.First();
            Assert.False(deviceResult.Success);
            Assert.Contains("validation failed", deviceResult.Message);
        }

        [Fact]
        public async Task ConfigureTelemetryLevel_ConfigureMode_QueuesCommand()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            // Add report with telemetry level = 0 (Security)
            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 0, optIn: false);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            var command = new TelemetryConfigurationCommand
            {
                RequiredTelemetryLevel = 1, // Basic
                ValidateOnly = false
            };
            var deviceIds = new List<Guid> { deviceId };

            // Act
            var result = await _controller.ConfigureTelemetryLevelAsync(command, deviceIds, null, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var batchResult = okResult.Value as DeviceConfigurationController.DeviceConfigurationBatchResult;
            Assert.NotNull(batchResult);
            Assert.Equal(1, batchResult.TotalDevices);
            Assert.Equal(1, batchResult.SuccessCount); // Command should be queued successfully
            Assert.Equal(0, batchResult.FailureCount);

            var deviceResult = batchResult.Results.First();
            Assert.True(deviceResult.Success);
            Assert.Contains("command queued", deviceResult.Message);
        }

        [Fact]
        public async Task GetDeviceState_DeviceNotFound_ReturnsNotFound()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Act
            var result = await _controller.GetDeviceStateAsync(deviceId, default);

            // Assert
            Assert.NotNull(result);
            var notFoundResult = Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>(result.Result);
            Assert.NotNull(notFoundResult.Value);
        }

        [Fact]
        public async Task GetDeviceState_ValidDevice_ReturnsState()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 2, optIn: true);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetDeviceStateAsync(deviceId, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var state = okResult.Value as DeviceConfigurationState;
            Assert.NotNull(state);
            Assert.Equal(true, state.MicrosoftUpdateManagedOptIn);
            Assert.Equal(2u, state.AllowTelemetry);
            Assert.Equal(1u, state.WindowsUEFICA2023Capable);
            Assert.True(state.IsCfrEligible);
        }

        [Fact]
        public async Task GetDeviceState_CfrNotEligible_ReturnsFalse()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-PC",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            _dbContext.Devices.Add(device);

            // Device with telemetry = 0 (Security), should not be CFR eligible even if opted in
            var report = CreateTestReport(deviceId, windowsUefiCa2023Capable: 1, telemetryLevel: 0, optIn: true);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetDeviceStateAsync(deviceId, default);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
            var state = okResult.Value as DeviceConfigurationState;
            Assert.NotNull(state);
            Assert.False(state.IsCfrEligible); // Should be false due to telemetry level 0
        }

        private SecureBootReportEntity CreateTestReport(
            Guid deviceId, 
            uint? windowsUefiCa2023Capable = null,
            uint? telemetryLevel = null,
            bool? optIn = null)
        {
            var registrySnapshot = new SecureBootRegistrySnapshot
            {
                MicrosoftUpdateManagedOptIn = optIn,
                WindowsUEFICA2023CapableCode = windowsUefiCa2023Capable,
                UEFISecureBootEnabled = true
            };

            var telemetrySnapshot = new TelemetryPolicySnapshot
            {
                AllowTelemetry = telemetryLevel
            };

            var statusReport = new SecureBootStatusReport
            {
                Registry = registrySnapshot,
                TelemetryPolicy = telemetrySnapshot,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var reportJson = JsonSerializer.Serialize(statusReport);

            return new SecureBootReportEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                RegistryStateJson = reportJson,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                DeploymentState = "Updated"
            };
        }
    }
}
