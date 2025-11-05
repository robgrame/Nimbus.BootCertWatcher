using System;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;
using SecureBootWatcher.Shared.Transport;

namespace SecureBootDashboard.Api.Services
{
    /// <summary>
    /// Background service that processes Secure Boot reports from Azure Queue Storage.
    /// Runs continuously, polling the queue and persisting messages to the database.
/// </summary>
    public sealed class QueueProcessorService : BackgroundService
    {
private readonly ILogger<QueueProcessorService> _logger;
private readonly IOptionsMonitor<QueueProcessorOptions> _optionsMonitor;
        private readonly IServiceProvider _serviceProvider;
      private QueueClient? _queueClient;

    public QueueProcessorService(
    ILogger<QueueProcessorService> logger,
IOptionsMonitor<QueueProcessorOptions> optionsMonitor,
            IServiceProvider serviceProvider)
        {
    _logger = logger;
   _optionsMonitor = optionsMonitor;
     _serviceProvider = serviceProvider;
        }

     public override Task StartAsync(CancellationToken cancellationToken)
        {
        var options = _optionsMonitor.CurrentValue;

     if (!options.Enabled)
 {
      _logger.LogInformation("Queue processor is disabled. Skipping startup.");
       return Task.CompletedTask;
            }

            _logger.LogInformation("Queue processor starting. Queue: {QueueName}, AuthMethod: {AuthMethod}",
         options.QueueName, options.AuthenticationMethod);

  _queueClient = CreateQueueClient(options);

            if (_queueClient == null)
{
       _logger.LogError("Failed to create queue client. Queue processor will not start.");
        return Task.CompletedTask;
   }

    return base.StartAsync(cancellationToken);
        }

     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
    var options = _optionsMonitor.CurrentValue;

   if (!options.Enabled || _queueClient == null)
            {
       return;
     }

            _logger.LogInformation("Queue processor started successfully.");

  while (!stoppingToken.IsCancellationRequested)
     {
      try
{
 await ProcessMessagesAsync(stoppingToken);
           }
        catch (Exception ex)
 {
        _logger.LogError(ex, "Error processing queue messages. Retrying after delay.");
        await Task.Delay(options.EmptyQueuePollInterval, stoppingToken);
        }
    }
  }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            var options = _optionsMonitor.CurrentValue;

            if (_queueClient == null)
{
       return;
       }

            // Receive messages from queue
            QueueMessage[]? messages = null;

          try
{
          var response = await _queueClient.ReceiveMessagesAsync(
                    maxMessages: options.MaxMessages,
            visibilityTimeout: options.VisibilityTimeout,
    cancellationToken: cancellationToken);

     messages = response.Value;
   }
            catch (RequestFailedException ex) when (ex.Status == 404)
     {
_logger.LogWarning("Queue {QueueName} does not exist. Waiting before retry.", options.QueueName);
      await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return;
            }
    catch (Exception ex)
            {
_logger.LogError(ex, "Failed to receive messages from queue {QueueName}", options.QueueName);
             await Task.Delay(options.EmptyQueuePollInterval, cancellationToken);
      return;
    }

     if (messages == null || messages.Length == 0)
      {
                // Queue is empty, wait longer before next poll
       await Task.Delay(options.EmptyQueuePollInterval, cancellationToken);
      return;
            }

  _logger.LogInformation("Received {MessageCount} message(s) from queue {QueueName}",
    messages.Length, options.QueueName);

   // Process each message
       foreach (var message in messages)
            {
      try
       {
          await ProcessSingleMessageAsync(message, cancellationToken);

    // Delete message after successful processing
          await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                    _logger.LogInformation("Successfully processed and deleted message {MessageId}", message.MessageId);
       }
catch (Exception ex)
     {
    _logger.LogError(ex, "Failed to process message {MessageId}. DequeueCount: {DequeueCount}",
         message.MessageId, message.DequeueCount);

     // Check if message has exceeded max dequeue count
       if (message.DequeueCount >= options.MaxDequeueCount)
   {
   _logger.LogWarning("Message {MessageId} exceeded max dequeue count ({MaxCount}). Consider moving to poison queue.",
  message.MessageId, options.MaxDequeueCount);

          // Optionally: Move to poison queue or delete
    // For now, we'll delete it to prevent infinite retries
             await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
          }
 // Otherwise, message will become visible again after VisibilityTimeout
     }
        }

     // If we processed messages, poll again immediately
      await Task.Delay(options.ProcessingInterval, cancellationToken);
        }

        private async Task ProcessSingleMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
       // Deserialize message
SecureBootQueueEnvelope? envelope = null;

         try
       {
 envelope = JsonSerializer.Deserialize<SecureBootQueueEnvelope>(
      message.MessageText,
     new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
 {
_logger.LogError(ex, "Failed to deserialize message {MessageId}. Invalid JSON format.", message.MessageId);
        throw;
     }

   if (envelope?.Report == null)
            {
        _logger.LogWarning("Message {MessageId} has null report. Skipping.", message.MessageId);
   throw new InvalidOperationException("Message envelope or report is null");
            }

            _logger.LogDebug("Processing report for device {MachineName} (MessageId: {MessageId})",
 envelope.Report.Device.MachineName, message.MessageId);

      // Save report using IReportStore (scoped service, so create scope)
     using var scope = _serviceProvider.CreateScope();
 var reportStore = scope.ServiceProvider.GetRequiredService<IReportStore>();

            var reportId = await reportStore.SaveAsync(envelope.Report, cancellationToken);

         _logger.LogInformation("Saved report {ReportId} for device {MachineName} from queue message {MessageId}",
        reportId, envelope.Report.Device.MachineName, message.MessageId);
        }

        private QueueClient? CreateQueueClient(QueueProcessorOptions options)
   {
   try
            {
  // Metodo 1: Connection String
       if (options.AuthenticationMethod.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrWhiteSpace(options.ConnectionString))
      {
        _logger.LogWarning("Using Connection String authentication. NOT recommended for production.");
  return new QueueClient(options.ConnectionString, options.QueueName);
       }

       // Valida QueueServiceUri per Entra ID auth
     if (options.QueueServiceUri == null)
        {
         _logger.LogError("QueueServiceUri is required for Entra ID authentication.");
   return null;
    }

       var queueUri = new Uri(options.QueueServiceUri, options.QueueName);
           TokenCredential credential;

      // Metodo 2: App Registration + Client Secret
        if (options.AuthenticationMethod.Equals("AppRegistration", StringComparison.OrdinalIgnoreCase))
       {
           if (string.IsNullOrWhiteSpace(options.TenantId) ||
       string.IsNullOrWhiteSpace(options.ClientId) ||
       string.IsNullOrWhiteSpace(options.ClientSecret))
   {
_logger.LogError("TenantId, ClientId, and ClientSecret are required for AppRegistration authentication.");
          return null;
       }

               _logger.LogInformation("Using App Registration authentication with Client ID: {ClientId}", options.ClientId);
     credential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
         return new QueueClient(queueUri, credential);
       }

   // Metodo 3: Certificate-based authentication
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
         _logger.LogInformation("Loaded certificate from file: {Path}", options.CertificatePath);
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
_logger.LogError("Certificate not found in store. Thumbprint: {Thumbprint}", options.CertificateThumbprint);
   return null;
         }

certificate = certificates[0];
      _logger.LogInformation("Loaded certificate from store. Thumbprint: {Thumbprint}, Subject: {Subject}",
certificate.Thumbprint, certificate.Subject);
  }

        if (certificate == null)
{
          _logger.LogError("Certificate could not be loaded.");
    return null;
         }

    _logger.LogInformation("Using Certificate-based authentication with Client ID: {ClientId}", options.ClientId);
  credential = new ClientCertificateCredential(options.TenantId, options.ClientId, certificate);
          return new QueueClient(queueUri, credential);
 }

    // Metodo 4: Managed Identity (raccomandato per Azure App Services)
      if (options.AuthenticationMethod.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase))
                {
           credential = string.IsNullOrWhiteSpace(options.ClientId)
               ? new ManagedIdentityCredential()
       : new ManagedIdentityCredential(options.ClientId);

          _logger.LogInformation("Using Managed Identity authentication");
    return new QueueClient(queueUri, credential);
    }

     // Metodo 5: DefaultAzureCredential
      if (options.AuthenticationMethod.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(options.AuthenticationMethod))
        {
     var credentialOptions = new DefaultAzureCredentialOptions();

  if (!string.IsNullOrWhiteSpace(options.ClientId))
          {
     credentialOptions.ManagedIdentityClientId = options.ClientId;
           }

          credential = new DefaultAzureCredential(credentialOptions);
 _logger.LogInformation("Using DefaultAzureCredential authentication");
         return new QueueClient(queueUri, credential);
   }

         _logger.LogError("Invalid AuthenticationMethod: {Method}", options.AuthenticationMethod);
          return null;
            }
            catch (Exception ex)
   {
       _logger.LogError(ex, "Failed to create QueueClient");
         return null;
            }
  }

    public override async Task StopAsync(CancellationToken cancellationToken)
     {
  _logger.LogInformation("Queue processor stopping...");
     await base.StopAsync(cancellationToken);
      _logger.LogInformation("Queue processor stopped.");
 }
    }
}
