using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootReportProxy.Functions.Configuration;
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
    private readonly ProxyFunctionOptions _options;
    private static QueueClient? _queueClient;
    private static readonly object _queueClientLock = new object();

    public SecureBootReportProxyFunction(
        ILogger<SecureBootReportProxyFunction> logger,
        IOptions<ProxyFunctionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
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
            // Validate API key authentication
            if (!ValidateApiKey(req))
            {
                _logger.LogWarning("API key authentication failed. CorrelationId={CorrelationId}", correlationId);
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Invalid or missing API key");
                return response;
            }

            _logger.LogInformation("API key authentication successful. CorrelationId={CorrelationId}", correlationId);

            // Validate certificate authentication if required
            if (_options.RequireCertificateAuthentication)
            {
                if (!ValidateCertificate(req))
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
            if (string.IsNullOrWhiteSpace(_options.QueueStorageUri))
            {
                _logger.LogError("QueueStorageUri is not configured. CorrelationId={CorrelationId}", correlationId);
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Queue storage is not configured");
                return response;
            }

            // Get or create queue client (with caching for performance)
            var queueClient = GetOrCreateQueueClient();

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
                    report.Device?.MachineName ?? "Unknown", _options.QueueName, correlationId);

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
                    _options.QueueName, correlationId);
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

    private bool ValidateApiKey(HttpRequestData req)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("API key is not configured in function settings");
            return false;
        }

        // Check X-API-Key header
        if (req.Headers.TryGetValues("X-API-Key", out var headerValues))
        {
            var providedKey = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(providedKey) && providedKey == _options.ApiKey)
            {
                return true;
            }
        }

        // Check query parameter (less secure, but supported for compatibility)
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var codeParam = query["code"];
        if (!string.IsNullOrWhiteSpace(codeParam) && codeParam == _options.ApiKey)
        {
            return true;
        }

        return false;
    }

    private bool ValidateCertificate(HttpRequestData req)
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
                if (_options.CertificateAuthentication.ValidateExpiration)
                {
                    var now = DateTime.Now;
                    if (now < certificate.NotBefore || now > certificate.NotAfter)
                    {
                        _logger.LogWarning("Client certificate is expired or not yet valid. NotBefore={NotBefore}, NotAfter={NotAfter}",
                            certificate.NotBefore, certificate.NotAfter);
                        return false;
                    }
                }

                // Validate certificate chain
                if (_options.CertificateAuthentication.ValidateCertificateChain)
                {
                    var chain = new X509Chain
                    {
                        ChainPolicy =
                        {
                            RevocationMode = _options.CertificateAuthentication.CheckCertificateRevocation 
                                ? X509RevocationMode.Online 
                                : X509RevocationMode.NoCheck
                        }
                    };

                    if (!chain.Build(certificate))
                    {
                        _logger.LogWarning("Certificate chain validation failed. ChainStatus={ChainStatus}",
                            string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation)));
                        return false;
                    }

                    // Validate Root CA if specified
                    if (!string.IsNullOrWhiteSpace(_options.CertificateAuthentication.ExpectedCARootName) ||
                        !string.IsNullOrWhiteSpace(_options.CertificateAuthentication.ExpectedCARootThumbprint))
                    {
                        var rootCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        
                        // Validate Root CA name
                        if (!string.IsNullOrWhiteSpace(_options.CertificateAuthentication.ExpectedCARootName))
                        {
                            if (!rootCert.Subject.Equals(_options.CertificateAuthentication.ExpectedCARootName, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogWarning("Root CA name mismatch. Expected={Expected}, Actual={Actual}",
                                    _options.CertificateAuthentication.ExpectedCARootName, rootCert.Subject);
                                return false;
                            }
                            _logger.LogDebug("Root CA name validated: {RootCAName}", rootCert.Subject);
                        }

                        // Validate Root CA thumbprint
                        if (!string.IsNullOrWhiteSpace(_options.CertificateAuthentication.ExpectedCARootThumbprint))
                        {
                            var expectedThumbprint = _options.CertificateAuthentication.ExpectedCARootThumbprint
                                .Replace(":", "").Replace(" ", "").ToUpperInvariant();
                            var actualThumbprint = rootCert.Thumbprint.ToUpperInvariant();

                            if (actualThumbprint != expectedThumbprint)
                            {
                                _logger.LogWarning("Root CA thumbprint mismatch. Expected={Expected}, Actual={Actual}",
                                    expectedThumbprint, actualThumbprint);
                                return false;
                            }
                            _logger.LogDebug("Root CA thumbprint validated: {Thumbprint}", actualThumbprint);
                        }
                    }

                    // Validate Subordinate CAs if specified
                    var expectedSubordinateCAs = _options.CertificateAuthentication.ExpectedSubordinateCAs;
                    if (expectedSubordinateCAs.Count > 0)
                    {
                        _logger.LogDebug("Validating {Count} expected Subordinate CAs", expectedSubordinateCAs.Count);

                        // Build list of intermediate CAs in chain (exclude root and leaf)
                        var intermediateCerts = new List<X509Certificate2>();
                        for (int i = 1; i < chain.ChainElements.Count - 1; i++)
                        {
                            intermediateCerts.Add(chain.ChainElements[i].Certificate);
                        }

                        foreach (var expectedCA in expectedSubordinateCAs)
                        {
                            var found = false;

                            foreach (var intermediateCert in intermediateCerts)
                            {
                                var nameMatch = string.IsNullOrWhiteSpace(expectedCA.Name) ||
                                    intermediateCert.Subject.Equals(expectedCA.Name, StringComparison.OrdinalIgnoreCase);

                                var thumbprintMatch = string.IsNullOrWhiteSpace(expectedCA.Thumbprint) ||
                                    intermediateCert.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant() ==
                                    expectedCA.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();

                                if (nameMatch && thumbprintMatch)
                                {
                                    found = true;
                                    _logger.LogDebug("Subordinate CA found in chain: {Subject}, Thumbprint={Thumbprint}",
                                        intermediateCert.Subject, intermediateCert.Thumbprint);
                                    break;
                                }
                            }

                            if (!found)
                            {
                                _logger.LogWarning("Expected Subordinate CA not found in chain: Name={Name}, Thumbprint={Thumbprint}",
                                    expectedCA.Name ?? "Not specified", expectedCA.Thumbprint ?? "Not specified");
                                return false;
                            }
                        }

                        _logger.LogInformation("All expected Subordinate CAs validated successfully");
                    }
                }

                // Validate thumbprint if allowlist is configured
                var allowedThumbprints = _options.CertificateAuthentication.AllowedThumbprintsArray;
                if (allowedThumbprints.Length > 0)
                {
                    var thumbprint = certificate.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
                    var isAllowed = allowedThumbprints.Any(allowed => allowed == thumbprint);

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

    private QueueClient GetOrCreateQueueClient()
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

            // Use Managed Identity (DefaultAzureCredential) for Azure Queue authentication
            var queueUri = new Uri(new Uri(_options.QueueStorageUri), _options.QueueName);
            _queueClient = new QueueClient(queueUri, new DefaultAzureCredential());

            _logger.LogInformation("QueueClient initialized. QueueUri={QueueUri}", queueUri);

            return _queueClient;
        }
    }
}
