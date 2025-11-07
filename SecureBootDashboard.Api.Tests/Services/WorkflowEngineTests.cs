using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Services
{
    public sealed class WorkflowEngineTests : IDisposable
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly WorkflowEngine _workflowEngine;
        private readonly IServiceProvider _serviceProvider;

        public WorkflowEngineTests()
        {
            var services = new ServiceCollection();
            
            services.AddDbContext<SecureBootDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
            
            services.AddLogging(builder => builder.AddConsole());
            services.AddScoped<WorkflowEngine>();
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<SecureBootDbContext>();
            _workflowEngine = _serviceProvider.GetRequiredService<WorkflowEngine>();
        }

        [Fact]
        public async Task EvaluateAndExecuteAsync_NoWorkflows_ReturnsEmptyList()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var reportId = Guid.NewGuid();

            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-DEVICE",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };

            var report = new SecureBootReportEntity
            {
                Id = reportId,
                DeviceId = deviceId,
                Device = device,
                RegistryStateJson = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.Devices.Add(device);
            _dbContext.Reports.Add(report);
            await _dbContext.SaveChangesAsync();

            // Act
            var executions = await _workflowEngine.EvaluateAndExecuteAsync(deviceId, reportId);

            // Assert
            Assert.Empty(executions);
        }

        [Fact]
        public async Task EvaluateAndExecuteAsync_WorkflowWithLogAction_ExecutesSuccessfully()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var reportId = Guid.NewGuid();

            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-DEVICE",
                FleetId = "test-fleet",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };

            var report = new SecureBootReportEntity
            {
                Id = reportId,
                DeviceId = deviceId,
                Device = device,
                RegistryStateJson = "{}",
                DeploymentState = "Error",
                AlertsJson = "[\"Test alert\"]",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var workflow = new RemediationWorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Workflow",
                IsEnabled = true,
                Priority = 100,
                TriggerJson = "{\"DeploymentState\":\"Error\",\"FleetIdMatches\":\"test-fleet\"}",
                ActionsJson = "[{\"ActionType\":3,\"ConfigurationJson\":\"{\\\"message\\\":\\\"Test log\\\"}\",\"Order\":1}]",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.Devices.Add(device);
            _dbContext.Reports.Add(report);
            _dbContext.RemediationWorkflows.Add(workflow);
            await _dbContext.SaveChangesAsync();

            // Act
            var executions = await _workflowEngine.EvaluateAndExecuteAsync(deviceId, reportId);

            // Assert
            Assert.Single(executions);
            var execution = executions.First();
            Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
            Assert.Equal(workflow.Id, execution.WorkflowId);
            Assert.Equal(deviceId, execution.DeviceId);
            Assert.Equal(reportId, execution.ReportId);
        }

        [Fact]
        public async Task EvaluateAndExecuteAsync_WorkflowWithNonMatchingTrigger_DoesNotExecute()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var reportId = Guid.NewGuid();

            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-DEVICE",
                FleetId = "other-fleet",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };

            var report = new SecureBootReportEntity
            {
                Id = reportId,
                DeviceId = deviceId,
                Device = device,
                RegistryStateJson = "{}",
                DeploymentState = "Updated",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var workflow = new RemediationWorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Workflow",
                IsEnabled = true,
                Priority = 100,
                TriggerJson = "{\"DeploymentState\":\"Error\",\"FleetIdMatches\":\"test-fleet\"}",
                ActionsJson = "[{\"ActionType\":3,\"ConfigurationJson\":\"{\\\"message\\\":\\\"Test log\\\"}\",\"Order\":1}]",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.Devices.Add(device);
            _dbContext.Reports.Add(report);
            _dbContext.RemediationWorkflows.Add(workflow);
            await _dbContext.SaveChangesAsync();

            // Act
            var executions = await _workflowEngine.EvaluateAndExecuteAsync(deviceId, reportId);

            // Assert
            Assert.Empty(executions);
        }

        [Fact]
        public async Task EvaluateAndExecuteAsync_DisabledWorkflow_DoesNotExecute()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var reportId = Guid.NewGuid();

            var device = new DeviceEntity
            {
                Id = deviceId,
                MachineName = "TEST-DEVICE",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };

            var report = new SecureBootReportEntity
            {
                Id = reportId,
                DeviceId = deviceId,
                Device = device,
                RegistryStateJson = "{}",
                DeploymentState = "Error",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var workflow = new RemediationWorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Workflow",
                IsEnabled = false, // Disabled
                Priority = 100,
                TriggerJson = "{\"DeploymentState\":\"Error\"}",
                ActionsJson = "[{\"ActionType\":3,\"ConfigurationJson\":\"{\\\"message\\\":\\\"Test log\\\"}\",\"Order\":1}]",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.Devices.Add(device);
            _dbContext.Reports.Add(report);
            _dbContext.RemediationWorkflows.Add(workflow);
            await _dbContext.SaveChangesAsync();

            // Act
            var executions = await _workflowEngine.EvaluateAndExecuteAsync(deviceId, reportId);

            // Assert
            Assert.Empty(executions);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }
    }
}
