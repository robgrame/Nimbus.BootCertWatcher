using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// API endpoints for managing certificate compliance policies.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PoliciesController : ControllerBase
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<PoliciesController> _logger;

        public PoliciesController(
            SecureBootDbContext dbContext,
            ILogger<PoliciesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get all compliance policies.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CertificateCompliancePolicy>>> GetAllPolicies()
        {
            try
            {
                var entities = await _dbContext.Policies
                    .OrderBy(p => p.Priority)
                    .ToListAsync();

                var policies = entities.Select(MapToModel).ToList();
                return Ok(policies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving policies");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a specific policy by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<CertificateCompliancePolicy>> GetPolicy(Guid id)
        {
            try
            {
                var entity = await _dbContext.Policies.FindAsync(id);
                if (entity == null)
                {
                    return NotFound();
                }

                var policy = MapToModel(entity);
                return Ok(policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving policy {PolicyId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Create a new compliance policy.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CertificateCompliancePolicy>> CreatePolicy(
            [FromBody] CertificateCompliancePolicy policy)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(policy.Name))
                {
                    return BadRequest("Policy name is required");
                }

                policy.Id = Guid.NewGuid();
                policy.CreatedAtUtc = DateTimeOffset.UtcNow;
                policy.ModifiedAtUtc = null;

                var entity = MapToEntity(policy);
                _dbContext.Policies.Add(entity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Created policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);
                return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating policy");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update an existing compliance policy.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<CertificateCompliancePolicy>> UpdatePolicy(
            Guid id,
            [FromBody] CertificateCompliancePolicy policy)
        {
            try
            {
                if (id != policy.Id)
                {
                    return BadRequest("Policy ID mismatch");
                }

                var entity = await _dbContext.Policies.FindAsync(id);
                if (entity == null)
                {
                    return NotFound();
                }

                policy.ModifiedAtUtc = DateTimeOffset.UtcNow;
                policy.CreatedAtUtc = entity.CreatedAtUtc; // Preserve original creation time

                var updatedEntity = MapToEntity(policy);
                _dbContext.Entry(entity).CurrentValues.SetValues(updatedEntity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);
                return Ok(policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating policy {PolicyId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Delete a compliance policy.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeletePolicy(Guid id)
        {
            try
            {
                var entity = await _dbContext.Policies.FindAsync(id);
                if (entity == null)
                {
                    return NotFound();
                }

                _dbContext.Policies.Remove(entity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Deleted policy {PolicyId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting policy {PolicyId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private static CertificateCompliancePolicy MapToModel(PolicyEntity entity)
        {
            var rules = new List<PolicyRule>();
            if (!string.IsNullOrEmpty(entity.RulesJson))
            {
                try
                {
                    rules = JsonSerializer.Deserialize<List<PolicyRule>>(entity.RulesJson) ?? new List<PolicyRule>();
                }
                catch
                {
                    // If deserialization fails, return empty list
                }
            }

            return new CertificateCompliancePolicy
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
            };
        }

        private static PolicyEntity MapToEntity(CertificateCompliancePolicy policy)
        {
            return new PolicyEntity
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description,
                IsEnabled = policy.IsEnabled,
                Priority = policy.Priority,
                FleetId = policy.FleetId,
                RulesJson = JsonSerializer.Serialize(policy.Rules),
                CreatedAtUtc = policy.CreatedAtUtc,
                ModifiedAtUtc = policy.ModifiedAtUtc
            };
        }
    }
}
