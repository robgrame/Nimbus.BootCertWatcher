using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SecureBootDashboard.Api.Controllers;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Controllers
{
    public class CertificateUpdateControllerTests
    {
        private readonly Mock<ICertificateUpdateService> _mockUpdateService;
        private readonly Mock<ILogger<CertificateUpdateController>> _mockLogger;
        private readonly CertificateUpdateController _controller;

        public CertificateUpdateControllerTests()
        {
            _mockUpdateService = new Mock<ICertificateUpdateService>();
            _mockLogger = new Mock<ILogger<CertificateUpdateController>>();
            _controller = new CertificateUpdateController(_mockUpdateService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task TriggerUpdateAsync_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new CertificateUpdateController.CertificateUpdateRequest
            {
                FleetId = "fleet-01",
                IssuedBy = "admin@example.com",
                Notes = "Test update"
            };

            var expectedResult = new CertificateUpdateResult(
                Guid.NewGuid(),
                10,
                "Update command sent successfully to 10 device(s)");

            _mockUpdateService
                .Setup(s => s.SendUpdateCommandAsync(It.IsAny<CertificateUpdateCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.TriggerUpdateAsync(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<CertificateUpdateResult>(okResult.Value);
            Assert.Equal(expectedResult.TargetDeviceCount, returnValue.TargetDeviceCount);
        }

        [Fact]
        public async Task TriggerUpdateAsync_ServiceThrowsException_Returns500()
        {
            // Arrange
            var request = new CertificateUpdateController.CertificateUpdateRequest
            {
                FleetId = "fleet-01",
                IssuedBy = "admin@example.com"
            };

            _mockUpdateService
                .Setup(s => s.SendUpdateCommandAsync(It.IsAny<CertificateUpdateCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.TriggerUpdateAsync(request, CancellationToken.None);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetCommandStatusAsync_ValidCommandId_ReturnsOkResult()
        {
            // Arrange
            var commandId = Guid.NewGuid();
            var expectedStatus = new CertificateUpdateCommandStatus(
                commandId,
                "fleet-01",
                10,
                5,
                DateTimeOffset.UtcNow,
                null,
                "InProgress");

            _mockUpdateService
                .Setup(s => s.GetCommandStatusAsync(commandId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStatus);

            // Act
            var result = await _controller.GetCommandStatusAsync(commandId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<CertificateUpdateCommandStatus>(okResult.Value);
            Assert.Equal(commandId, returnValue.CommandId);
        }

        [Fact]
        public async Task GetCommandStatusAsync_CommandNotFound_ReturnsNotFound()
        {
            // Arrange
            var commandId = Guid.NewGuid();

            _mockUpdateService
                .Setup(s => s.GetCommandStatusAsync(commandId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CertificateUpdateCommandStatus?)null);

            // Act
            var result = await _controller.GetCommandStatusAsync(commandId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task TriggerUpdateAsync_WithTargetDevices_SendsCorrectCommand()
        {
            // Arrange
            var targetDevices = new[] { "DEVICE-01", "DEVICE-02" };
            var request = new CertificateUpdateController.CertificateUpdateRequest
            {
                FleetId = "fleet-01",
                TargetDevices = targetDevices,
                IssuedBy = "admin@example.com",
                UpdateFlags = 0x5944
            };

            CertificateUpdateCommand? capturedCommand = null;
            _mockUpdateService
                .Setup(s => s.SendUpdateCommandAsync(It.IsAny<CertificateUpdateCommand>(), It.IsAny<CancellationToken>()))
                .Callback<CertificateUpdateCommand, CancellationToken>((cmd, ct) => capturedCommand = cmd)
                .ReturnsAsync(new CertificateUpdateResult(Guid.NewGuid(), 2, "Success"));

            // Act
            await _controller.TriggerUpdateAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal("fleet-01", capturedCommand.FleetId);
            Assert.Equal(targetDevices, capturedCommand.TargetDevices);
            Assert.Equal((uint)0x5944, capturedCommand.UpdateFlags);
            Assert.Equal("admin@example.com", capturedCommand.IssuedBy);
        }
    }
}
