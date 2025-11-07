using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// API endpoints for device compliance evaluation.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ComplianceController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<ComplianceController> _logger;

        public ComplianceController(
            SecureBootDbContext dbContext,
            ILogger<ComplianceController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Evaluate compliance for a specific device using its latest report.
        /// </summary>
        [HttpGet("devices/{deviceId}")]
        public async Task<ActionResult<ComplianceResult>> EvaluateDevice(Guid deviceId)
        {
            try
            {
                var device = await _dbContext.Devices
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .FirstOrDefaultAsync(d => d.Id == deviceId);

                if (device == null)
                {
                    return NotFound($"Device {deviceId} not found");
                }

                var latestReport = device.Reports.FirstOrDefault();
                if (latestReport == null)
                {
                    return NotFound($"No reports found for device {deviceId}");
                }

                // Deserialize certificates
                SecureBootCertificateCollection? certificates = null;
                if (!string.IsNullOrEmpty(latestReport.CertificatesJson))
                {
                    try
                    {
                        certificates = JsonSerializer.Deserialize<SecureBootCertificateCollection>(latestReport.CertificatesJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize certificates for device {DeviceId}", deviceId);
                    }
                }

                // Load policies
                var policies = await LoadPoliciesAsync();

                // Evaluate compliance
                var evaluationService = new PolicyEvaluationService(policies);
                var result = evaluationService.EvaluateCompliance(
                    deviceId,
                    latestReport.Id,
                    certificates,
                    device.FleetId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating compliance for device {DeviceId}", deviceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Evaluate compliance for all devices.
        /// </summary>
        [HttpGet("devices")]
        public async Task<ActionResult<IEnumerable<ComplianceResult>>> EvaluateAllDevices()
        {
            try
            {
                var devices = await _dbContext.Devices
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .ToListAsync();

                // Load policies once
                var policies = await LoadPoliciesAsync();
                var evaluationService = new PolicyEvaluationService(policies);

                var results = new List<ComplianceResult>();

                foreach (var device in devices)
                {
                    var latestReport = device.Reports.FirstOrDefault();
                    if (latestReport == null)
                    {
                        continue;
                    }

                    // Deserialize certificates
                    SecureBootCertificateCollection? certificates = null;
                    if (!string.IsNullOrEmpty(latestReport.CertificatesJson))
                    {
                        try
                        {
                            certificates = JsonSerializer.Deserialize<SecureBootCertificateCollection>(latestReport.CertificatesJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize certificates for device {DeviceId}", device.Id);
                        }
                    }

                    var result = evaluationService.EvaluateCompliance(
                        device.Id,
                        latestReport.Id,
                        certificates,
                        device.FleetId);

                    results.Add(result);
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating compliance for all devices");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get compliance summary statistics.
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ComplianceSummary>> GetComplianceSummary()
        {
            try
            {
                var devices = await _dbContext.Devices
                    .Include(d => d.Reports.OrderByDescending(r => r.CreatedAtUtc).Take(1))
                    .ToListAsync();

                var policies = await LoadPoliciesAsync();
                var evaluationService = new PolicyEvaluationService(policies);

                var summary = new ComplianceSummary();

                foreach (var device in devices)
                {
                    var latestReport = device.Reports.FirstOrDefault();
                    if (latestReport == null)
                    {
                        summary.UnknownCount++;
                        continue;
                    }

                    SecureBootCertificateCollection? certificates = null;
                    if (!string.IsNullOrEmpty(latestReport.CertificatesJson))
                    {
                        try
                        {
                            certificates = JsonSerializer.Deserialize<SecureBootCertificateCollection>(latestReport.CertificatesJson);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize certificates for device {DeviceId}, report {ReportId}", 
                                device.Id, latestReport.Id);
                        }
                    }

                    var result = evaluationService.EvaluateCompliance(
                        device.Id,
                        latestReport.Id,
                        certificates,
                        device.FleetId);

                    switch (result.Status)
                    {
                        case ComplianceStatus.Compliant:
                            summary.CompliantCount++;
                            break;
                        case ComplianceStatus.Warning:
                            summary.WarningCount++;
                            break;
                        case ComplianceStatus.NonCompliant:
                            summary.NonCompliantCount++;
                            break;
                        default:
                            summary.UnknownCount++;
                            break;
                    }
                }

                summary.TotalDevices = devices.Count;
                summary.EvaluatedAtUtc = DateTimeOffset.UtcNow;

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance summary");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<List<CertificateCompliancePolicy>> LoadPoliciesAsync()
        {
            var policyEntities = await _dbContext.Policies
                .Where(p => p.IsEnabled)
                .OrderBy(p => p.Priority)
                .ToListAsync();

            var policies = new List<CertificateCompliancePolicy>();

            foreach (var entity in policyEntities)
            {
                var rules = new List<PolicyRule>();
                if (!string.IsNullOrEmpty(entity.RulesJson))
                {
                    try
                    {
                        rules = JsonSerializer.Deserialize<List<PolicyRule>>(entity.RulesJson) ?? new List<PolicyRule>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize rules for policy {PolicyId}", entity.Id);
                    }
                }

                policies.Add(new CertificateCompliancePolicy
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Description = entity.Description,
                    IsEnabled = entity.IsEnabled,
                    Priority = entity.Priority,
                    FleetId = entity.FleetId,
                    Rules = rules,
                    CreatedAtUtc = entity.CreatedAtUtc,
                    ModifiedAtUtc = entity.ModifiedAtUtc
                });
            }

            return policies;
        }
    }

    /// <summary>
    /// Summary of compliance status across all devices.
    /// </summary>
    public sealed class ComplianceSummary
    {
        public int TotalDevices { get; set; }
        public int CompliantCount { get; set; }
        public int WarningCount { get; set; }
        public int NonCompliantCount { get; set; }
        public int UnknownCount { get; set; }
        public DateTimeOffset EvaluatedAtUtc { get; set; }
    }
}
