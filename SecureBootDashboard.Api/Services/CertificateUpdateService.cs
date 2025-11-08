using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Transport;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Service for managing certificate update commands via Azure Queue Storage.
    /// </summary>
    public sealed class CertificateUpdateService : ICertificateUpdateService
    {
        private readonly SecureBootDbContext _dbContext;
        private readonly ILogger<CertificateUpdateService> _logger;
        private readonly IOptionsMonitor<CertificateUpdateServiceOptions> _optionsMonitor;
        private QueueClient? _commandQueueClient;

        public CertificateUpdateService(
            SecureBootDbContext dbContext,
            ILogger<CertificateUpdateService> logger,
            IOptionsMonitor<CertificateUpdateServiceOptions> optionsMonitor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
        }

        public async Task<CertificateUpdateResult> SendUpdateCommandAsync(
            CertificateUpdateCommand command,
            CancellationToken cancellationToken = default)
        {
            var options = _optionsMonitor.CurrentValue;

            if (!options.Enabled)
            {
                _logger.LogWarning("Certificate update service is disabled");
                return new CertificateUpdateResult(
                    command.CommandId,
                    0,
                    "Certificate update service is disabled");
            }

            // Determine target devices
            var targetDevices = await GetTargetDevicesAsync(command, cancellationToken);
            var targetCount = targetDevices.Count;

            _logger.LogInformation(
                "Certificate update command {CommandId} targeting {Count} devices in fleet {FleetId}",
                command.CommandId,
                targetCount,
                command.FleetId ?? "ALL");

            if (targetCount == 0)
            {
                return new CertificateUpdateResult(
                    command.CommandId,
                    0,
                    "No devices found matching the criteria");
            }

            // Initialize queue client if needed
            if (_commandQueueClient == null)
            {
                _commandQueueClient = CreateQueueClient(options);
                if (_commandQueueClient == null)
                {
                    throw new InvalidOperationException("Failed to create queue client");
                }
            }

            // Send command to queue
            var envelope = new CertificateUpdateCommandEnvelope
            {
                Command = command,
                EnqueuedAtUtc = DateTimeOffset.UtcNow
            };

            var messageJson = JsonSerializer.Serialize(envelope);

            try
            {
                await _commandQueueClient.SendMessageAsync(messageJson, cancellationToken);

                _logger.LogInformation(
                    "Certificate update command {CommandId} sent to queue for {Count} devices",
                    command.CommandId,
                    targetCount);

                return new CertificateUpdateResult(
                    command.CommandId,
                    targetCount,
                    $"Update command sent successfully to {targetCount} device(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send certificate update command {CommandId}", command.CommandId);
                throw;
            }
        }

        public Task<CertificateUpdateCommandStatus?> GetCommandStatusAsync(
            Guid commandId,
            CancellationToken cancellationToken = default)
        {
            // Note: This is a basic implementation. In a production system, you would:
            // 1. Store command metadata in a database table
            // 2. Track device acknowledgments and completion
            // 3. Monitor queue message processing
            
            // For now, return a placeholder status
            _logger.LogInformation("Getting status for command {CommandId}", commandId);
            
            // This would be enhanced to query a commands table in the database
            var status = new CertificateUpdateCommandStatus(
                commandId,
                null,
                0,
                0,
                DateTimeOffset.UtcNow,
                null,
                "Pending");
                
            return Task.FromResult<CertificateUpdateCommandStatus?>(status);
        }

        private async Task<System.Collections.Generic.List<string>> GetTargetDevicesAsync(
            CertificateUpdateCommand command,
            CancellationToken cancellationToken)
        {
            IQueryable<DeviceEntity> query = _dbContext.Devices;

            // Filter by fleet if specified
            if (!string.IsNullOrWhiteSpace(command.FleetId))
            {
                query = query.Where(d => d.FleetId == command.FleetId);
            }

            // Filter by specific devices if specified
            if (command.TargetDevices != null && command.TargetDevices.Length > 0)
            {
                query = query.Where(d => command.TargetDevices.Contains(d.MachineName));
            }

            var devices = await query
                .Select(d => d.MachineName)
                .ToListAsync(cancellationToken);

            return devices;
        }

        private QueueClient? CreateQueueClient(CertificateUpdateServiceOptions options)
        {
            try
            {
                // Connection String method
                if (options.AuthenticationMethod.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    _logger.LogWarning("Using Connection String authentication. NOT recommended for production.");
                    return new QueueClient(options.ConnectionString, options.CommandQueueName);
                }

                // Validate QueueServiceUri for Entra ID auth
                if (options.QueueServiceUri == null)
                {
                    _logger.LogError("QueueServiceUri is required for Entra ID authentication.");
                    return null;
                }

                var queueUri = new Uri(options.QueueServiceUri, options.CommandQueueName);
                TokenCredential credential;

                // App Registration + Client Secret
                if (options.AuthenticationMethod.Equals("AppRegistration", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(options.TenantId) ||
                        string.IsNullOrWhiteSpace(options.ClientId) ||
                        string.IsNullOrWhiteSpace(options.ClientSecret))
                    {
                        _logger.LogError("TenantId, ClientId, and ClientSecret are required for AppRegistration authentication.");
                        return null;
                    }

                    _logger.LogInformation("Using App Registration authentication for command queue");
                    credential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
                    return new QueueClient(queueUri, credential);
                }

                // Certificate-based authentication
                if (options.AuthenticationMethod.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.ClientId))
                    {
                        _logger.LogError("TenantId and ClientId are required for Certificate authentication.");
                        return null;
                    }

                    X509Certificate2? certificate = null;

                    // From file
                    if (!string.IsNullOrWhiteSpace(options.CertificatePath))
                    {
                        var password = options.CertificatePassword;
                        certificate = string.IsNullOrWhiteSpace(password)
                            ? new X509Certificate2(options.CertificatePath)
                            : new X509Certificate2(options.CertificatePath, password);
                        _logger.LogInformation("Loaded certificate from file for command queue");
                    }
                    // From Windows Certificate Store
                    else if (!string.IsNullOrWhiteSpace(options.CertificateThumbprint))
                    {
                        var storeLocation = options.CertificateStoreLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                            ? StoreLocation.LocalMachine
                            : StoreLocation.CurrentUser;

                        var storeName = Enum.TryParse<StoreName>(options.CertificateStoreName, true, out var parsed)
                            ? parsed
                            : StoreName.My;

                        using var store = new X509Store(storeName, storeLocation);
                        store.Open(OpenFlags.ReadOnly);

                        var certificates = store.Certificates.Find(
                            X509FindType.FindByThumbprint,
                            options.CertificateThumbprint.Replace(" ", "").Replace(":", ""),
                            validOnly: false);

                        if (certificates.Count == 0)
                        {
                            _logger.LogError("Certificate not found in store");
                            return null;
                        }

                        certificate = certificates[0];
                        _logger.LogInformation("Loaded certificate from store for command queue");
                    }

                    if (certificate == null)
                    {
                        _logger.LogError("Certificate could not be loaded.");
                        return null;
                    }

                    credential = new ClientCertificateCredential(options.TenantId, options.ClientId, certificate);
                    return new QueueClient(queueUri, credential);
                }

                // Managed Identity
                if (options.AuthenticationMethod.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase))
                {
                    credential = string.IsNullOrWhiteSpace(options.ClientId)
                        ? new ManagedIdentityCredential()
                        : new ManagedIdentityCredential(options.ClientId);

                    _logger.LogInformation("Using Managed Identity for command queue");
                    return new QueueClient(queueUri, credential);
                }

                // DefaultAzureCredential
                if (options.AuthenticationMethod.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(options.AuthenticationMethod))
                {
                    var credentialOptions = new DefaultAzureCredentialOptions();

                    if (!string.IsNullOrWhiteSpace(options.ClientId))
                    {
                        credentialOptions.ManagedIdentityClientId = options.ClientId;
                    }

                    credential = new DefaultAzureCredential(credentialOptions);
                    _logger.LogInformation("Using DefaultAzureCredential for command queue");
                    return new QueueClient(queueUri, credential);
                }

                _logger.LogError("Invalid AuthenticationMethod: {Method}", options.AuthenticationMethod);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create command queue client");
                return null;
            }
        }
    }
}
