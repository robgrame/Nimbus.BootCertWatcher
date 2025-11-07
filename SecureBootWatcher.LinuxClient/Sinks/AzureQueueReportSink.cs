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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Transport;

namespace SecureBootWatcher.LinuxClient.Sinks
{
    internal sealed class AzureQueueReportSink : IReportSink
  {
   private readonly ILogger<AzureQueueReportSink> _logger;
 private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly AsyncRetryPolicy _retryPolicy;

  public AzureQueueReportSink(ILogger<AzureQueueReportSink> logger, IOptionsMonitor<SecureBootWatcherOptions> options)
   {
       _logger = logger;
  _options = options;
            _retryPolicy = Policy
      .Handle<RequestFailedException>()
          .Or<TimeoutException>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), (ex, span, attempt, _) =>
   {
          _logger.LogWarning(ex, "Retrying Azure Queue send attempt {Attempt} after {Delay}.", attempt, span);
    });
 }

        public async Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
     {
   var sinkOptions = _options.CurrentValue.Sinks.AzureQueue;
       if (string.IsNullOrWhiteSpace(sinkOptions.QueueName))
      {
           _logger.LogDebug("Azure Queue sink is disabled because QueueName is not configured.");
    return;
   }

 var queueClient = CreateQueueClient(sinkOptions);
    if (queueClient == null)
      {
        _logger.LogWarning("Azure Queue sink skipped because required configuration is missing.");
    return;
   }

      await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var envelope = new SecureBootQueueEnvelope
  {
  Report = report,
             EnqueuedAtUtc = DateTimeOffset.UtcNow
            };

     var payload = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
    });

            await _retryPolicy.ExecuteAsync(async token =>
        {
          await queueClient.SendMessageAsync(
       BinaryData.FromString(payload),
       visibilityTimeout: sinkOptions.VisibilityTimeout,
     cancellationToken: token).ConfigureAwait(false);

      _logger.LogInformation("Secure Boot report enqueued to {QueueName} using {AuthMethod} authentication.", 
        queueClient.Name, sinkOptions.AuthenticationMethod);
      }, cancellationToken).ConfigureAwait(false);
        }

   private QueueClient? CreateQueueClient(AzureQueueSinkOptions options)
        {
            try
  {
     // Method 1: Connection String (not recommended for production)
    if (options.AuthenticationMethod.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(options.ConnectionString))
        {
     _logger.LogWarning("Using Connection String authentication. This is NOT recommended for production. Use App Registration or Managed Identity instead.");
 return new QueueClient(options.ConnectionString, options.QueueName);
 }

  // Validate that QueueServiceUri is configured for Entra ID authentication
             if (options.QueueServiceUri == null)
          {
       _logger.LogError("QueueServiceUri is required for Entra ID authentication.");
   return null;
            }

  var queueUri = new Uri(options.QueueServiceUri, options.QueueName);
      TokenCredential credential;

       // Method 2: App Registration with Client Secret (recommended for service-to-service)
  if (options.AuthenticationMethod.Equals("AppRegistration", StringComparison.OrdinalIgnoreCase))
           {
      if (string.IsNullOrWhiteSpace(options.TenantId) ||
      string.IsNullOrWhiteSpace(options.ClientId) ||
      string.IsNullOrWhiteSpace(options.ClientSecret))
        {
   _logger.LogError("TenantId, ClientId, and ClientSecret are required for App Registration authentication.");
return null;
            }

        _logger.LogInformation("Using App Registration authentication with Client ID: {ClientId}", options.ClientId);
          credential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
       return new QueueClient(queueUri, credential);
     }

     // Metodo 3: Certificate-based authentication (PIÙ SICURO - raccomandato per produzione)
       if (options.AuthenticationMethod.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
      {
               if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.ClientId))
         {
              _logger.LogError("TenantId and ClientId are required for Certificate authentication.");
            return null;
      }

         X509Certificate2? certificate = null;

          // Option A: Load certificate from file
     if (!string.IsNullOrWhiteSpace(options.CertificatePath))
    {
       try
      {
            if (!string.IsNullOrWhiteSpace(options.CertificatePassword))
  {
         certificate = new X509Certificate2(options.CertificatePath, options.CertificatePassword);
          _logger.LogInformation("Loaded certificate from file: {Path}", options.CertificatePath);
      }
  else
     {
         certificate = new X509Certificate2(options.CertificatePath);
 _logger.LogInformation("Loaded certificate from file (no password): {Path}", options.CertificatePath);
}
    }
               catch (Exception ex)
 {
       _logger.LogError(ex, "Failed to load certificate from file: {Path}", options.CertificatePath);
              return null;
        }
         }
          // Opzione B: Carica certificato da Windows Certificate Store
        else if (!string.IsNullOrWhiteSpace(options.CertificateThumbprint))
   {
           try
           {
               var storeLocation = options.CertificateStoreLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
  ? StoreLocation.LocalMachine
       : StoreLocation.CurrentUser;

          var storeName = Enum.TryParse<StoreName>(options.CertificateStoreName, true, out var parsedStoreName)
   ? parsedStoreName
  : StoreName.My;

        using var store = new X509Store(storeName, storeLocation);
          store.Open(OpenFlags.ReadOnly);

           var certificates = store.Certificates.Find(
                X509FindType.FindByThumbprint,
   options.CertificateThumbprint?.Replace(" ", "").Replace(":", "") ?? string.Empty,
   validOnly: false);

             if (certificates.Count == 0)
         {
    _logger.LogError("Certificate not found in store. Thumbprint: {Thumbprint}, Location: {Location}, Store: {Store}",
options.CertificateThumbprint, storeLocation, storeName);
              return null;
      }

        certificate = certificates[0];
        _logger.LogInformation("Loaded certificate from store. Thumbprint: {Thumbprint}, Subject: {Subject}",
    certificate.Thumbprint, certificate.Subject);
    }
         catch (Exception ex)
{
       _logger.LogError(ex, "Failed to load certificate from store. Thumbprint: {Thumbprint}", options.CertificateThumbprint);
    return null;
     }
       }
       else
   {
    _logger.LogError("Either CertificatePath or CertificateThumbprint must be specified for Certificate authentication.");
            return null;
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

      // Metodo 4: Managed Identity (raccomandato per Azure VMs, App Services, ecc.)
   if (options.AuthenticationMethod.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase))
        {
              if (!string.IsNullOrWhiteSpace(options.ClientId))
      {
      // User-Assigned Managed Identity
        _logger.LogInformation("Using User-Assigned Managed Identity with Client ID: {ClientId}", options.ClientId);
        credential = new ManagedIdentityCredential(options.ClientId);
         }
    else
        {
     // System-Assigned Managed Identity
      _logger.LogInformation("Using System-Assigned Managed Identity");
       credential = new ManagedIdentityCredential();
           }
 
       return new QueueClient(queueUri, credential);
     }

   // Metodo 5: DefaultAzureCredential (prova più metodi automaticamente)
    if (options.AuthenticationMethod.Equals("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase) ||
     string.IsNullOrWhiteSpace(options.AuthenticationMethod))
      {
        _logger.LogInformation("Using DefaultAzureCredential (tries Managed Identity, Azure CLI, Visual Studio, Environment Variables, etc.)");
           
  var credentialOptions = new DefaultAzureCredentialOptions();
       
    // Se è specificato un ClientId, usa quello per Managed Identity
 if (!string.IsNullOrWhiteSpace(options.ClientId))
      {
             credentialOptions.ManagedIdentityClientId = options.ClientId;
         _logger.LogInformation("Configured DefaultAzureCredential with Managed Identity Client ID: {ClientId}", options.ClientId);
          }

        credential = new DefaultAzureCredential(credentialOptions);
          return new QueueClient(queueUri, credential);
     }

       _logger.LogError("Invalid AuthenticationMethod: {Method}. Valid values are: ManagedIdentity, AppRegistration, Certificate, DefaultAzureCredential, ConnectionString.", 
        options.AuthenticationMethod);
        return null;
 }
     catch (Exception ex)
      {
      _logger.LogError(ex, "Failed to create Azure Queue client with authentication method: {Method}", options.AuthenticationMethod);
      return null;
      }
        }
    }
}
