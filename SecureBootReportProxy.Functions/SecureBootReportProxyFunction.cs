using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Transport;

namespace SecureBootReportProxy.Functions;

/// <summary>
/// Azure Function that acts as a proxy for Secure Boot reports.
/// Accepts reports via HTTP with API key authentication (and optional certificate authentication),
/// then forwards them to Azure Queue Storage using Managed Identity.
/// This eliminates the need to distribute Azure Storage credentials to client machines.
/// </summary>
public class SecureBootReportProxyFunction
{
    private readonly ILogger<SecureBootReportProxyFunction> _logger;
    private static QueueClient? _queueClient;
    private static readonly object _queueClientLock = new object();

    public SecureBootReportProxyFunction(ILogger<SecureBootReportProxyFunction> logger)
    {
        _logger = logger;
    }

    [Function("SecureBootReportIngestion")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reports")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("SecureBootReportIngestion function started. CorrelationId={CorrelationId}", correlationId);

        try
        {
            // Get configuration from environment variables
            var apiKey = Environment.GetEnvironmentVariable("ApiKey");
            var queueStorageUri = Environment.GetEnvironmentVariable("QueueStorageUri");
            var queueName = Environment.GetEnvironmentVariable("QueueName") ?? "secureboot-reports";
            var requireCertAuth = bool.Parse(Environment.GetEnvironmentVariable("RequireCertificateAuthentication") ?? "false");
            var allowedThumbprints = Environment.GetEnvironmentVariable("CertificateThumbprints")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();

            // Validate API key authentication
            if (!ValidateApiKey(req, apiKey))
            {
                _logger.LogWarning("API key authentication failed. CorrelationId={CorrelationId}", correlationId);
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Invalid or missing API key");
                return response;
            }

            _logger.LogInformation("API key authentication successful. CorrelationId={CorrelationId}", correlationId);

            // Validate certificate authentication if required
            if (requireCertAuth)
            {
                if (!ValidateCertificate(req, allowedThumbprints))
                {
                    _logger.LogWarning("Certificate authentication failed. CorrelationId={CorrelationId}", correlationId);
                    var response = req.CreateResponse(HttpStatusCode.Forbidden);
                    await response.WriteStringAsync("Invalid or missing client certificate");
                    return response;
                }

                _logger.LogInformation("Certificate authentication successful. CorrelationId={CorrelationId}", correlationId);
            }

            // Parse request body
            SecureBootStatusReport? report;
            try
            {
                var requestBody = await req.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    _logger.LogWarning("Empty request body. CorrelationId={CorrelationId}", correlationId);
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("Request body is empty");
                    return response;
                }

                report = JsonSerializer.Deserialize<SecureBootStatusReport>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (report == null)
                {
                    _logger.LogWarning("Failed to deserialize report. CorrelationId={CorrelationId}", correlationId);
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("Invalid report format");
                    return response;
                }

                _logger.LogInformation("Report deserialized successfully. Device={DeviceName}, CorrelationId={CorrelationId}",
                    report.Device?.MachineName ?? "Unknown", correlationId);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error. CorrelationId={CorrelationId}", correlationId);
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return response;
            }

            // Validate queue configuration
            if (string.IsNullOrWhiteSpace(queueStorageUri))
            {
                _logger.LogError("QueueStorageUri is not configured. CorrelationId={CorrelationId}", correlationId);
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Queue storage is not configured");
                return response;
            }

            // Get or create queue client (with caching for performance)
            var queueClient = GetOrCreateQueueClient(queueStorageUri, queueName);

            // Create queue envelope
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

            // Send to Azure Queue
            try
            {
                await queueClient.SendMessageAsync(payload);
                _logger.LogInformation("Report forwarded to Azure Queue successfully. Device={DeviceName}, Queue={QueueName}, CorrelationId={CorrelationId}",
                    report.Device?.MachineName ?? "Unknown", queueName, correlationId);

                var successResponse = req.CreateResponse(HttpStatusCode.Accepted);
                await successResponse.WriteAsJsonAsync(new
                {
                    status = "accepted",
                    message = "Report queued for processing",
                    correlationId = correlationId
                });
                return successResponse;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to send message to Azure Queue. Queue={QueueName}, CorrelationId={CorrelationId}",
                    queueName, correlationId);
                var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await response.WriteStringAsync($"Failed to queue report: {ex.Message}");
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SecureBootReportIngestion function. CorrelationId={CorrelationId}", correlationId);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Internal server error: {ex.Message}");
            return response;
        }
    }

    private bool ValidateApiKey(HttpRequestData req, string? expectedApiKey)
    {
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            _logger.LogWarning("API key is not configured in function settings");
            return false;
        }

        // Check X-API-Key header
        if (req.Headers.TryGetValues("X-API-Key", out var headerValues))
        {
            var providedKey = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(providedKey) && providedKey == expectedApiKey)
            {
                return true;
            }
        }

        // Check query parameter (less secure, but supported for compatibility)
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var codeParam = query["code"];
        if (!string.IsNullOrWhiteSpace(codeParam) && codeParam == expectedApiKey)
        {
            return true;
        }

        return false;
    }

    private bool ValidateCertificate(HttpRequestData req, string[] allowedThumbprints)
    {
        // Note: In Azure Functions, client certificates are validated by Azure App Service
        // if mutual TLS is enabled. The certificate information is passed via headers.
        
        // Check for client certificate header (set by Azure App Service when mutual TLS is enabled)
        if (req.Headers.TryGetValues("X-ARR-ClientCert", out var certValues))
        {
            var certHeader = certValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(certHeader))
            {
                _logger.LogWarning("X-ARR-ClientCert header is present but empty");
                return false;
            }

            try
            {
                // Decode the certificate from base64
                var certBytes = Convert.FromBase64String(certHeader);
                var certificate = new X509Certificate2(certBytes);

                _logger.LogInformation("Client certificate present. Subject={Subject}, Issuer={Issuer}, Thumbprint={Thumbprint}",
                    certificate.Subject, certificate.Issuer, certificate.Thumbprint);

                // Validate certificate expiration
                var now = DateTime.Now;
                if (now < certificate.NotBefore || now > certificate.NotAfter)
                {
                    _logger.LogWarning("Client certificate is expired or not yet valid. NotBefore={NotBefore}, NotAfter={NotAfter}",
                        certificate.NotBefore, certificate.NotAfter);
                    return false;
                }

                // Validate thumbprint if allowlist is configured
                if (allowedThumbprints.Length > 0)
                {
                    var thumbprint = certificate.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
                    var isAllowed = allowedThumbprints.Any(allowed =>
                        allowed.Replace(":", "").Replace(" ", "").ToUpperInvariant() == thumbprint);

                    if (!isAllowed)
                    {
                        _logger.LogWarning("Client certificate thumbprint {Thumbprint} is not in allowlist", thumbprint);
                        return false;
                    }

                    _logger.LogInformation("Client certificate thumbprint validated against allowlist");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse client certificate");
                return false;
            }
        }

        _logger.LogWarning("No client certificate found in request headers");
        return false;
    }

    private QueueClient GetOrCreateQueueClient(string queueStorageUri, string queueName)
    {
        if (_queueClient != null)
        {
            return _queueClient;
        }

        lock (_queueClientLock)
        {
            if (_queueClient != null)
            {
                return _queueClient;
            }

            _logger.LogInformation("Creating Azure Queue client. QueueUri={QueueUri}, QueueName={QueueName}",
                queueStorageUri, queueName);

            var queueUri = new Uri(new Uri(queueStorageUri), queueName);
            
            // Use DefaultAzureCredential which supports Managed Identity, Azure CLI, Visual Studio, etc.
            var credential = new DefaultAzureCredential();
            
            _queueClient = new QueueClient(queueUri, credential);
            
            // Create queue if it doesn't exist (requires appropriate permissions)
            try
            {
                _queueClient.CreateIfNotExists();
                _logger.LogInformation("Queue client created successfully. Queue={QueueName}", queueName);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Queue already exists, ignore
                _logger.LogInformation("Queue already exists. Queue={QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create queue (might already exist or lack permissions). Queue={QueueName}", queueName);
            }

            return _queueClient;
        }
    }
}
