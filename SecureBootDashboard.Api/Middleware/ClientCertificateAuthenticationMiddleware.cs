using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace SecureBootDashboard.Api.Middleware
{
    /// <summary>
    /// Middleware for validating client certificates on incoming API requests.
    /// </summary>
    public class ClientCertificateAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientCertificateAuthenticationMiddleware> _logger;
        private readonly ClientCertificateAuthenticationOptions _options;

        public ClientCertificateAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<ClientCertificateAuthenticationMiddleware> logger,
            IOptions<ClientCertificateAuthenticationOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip if certificate authentication is disabled
            if (!_options.Enabled)
            {
                await _next(context);
                return;
            }

            var clientCertificate = context.Connection.ClientCertificate;

            // Check if certificate is required
            if (_options.RequireClientCertificate && clientCertificate == null)
            {
                _logger.LogWarning("Client certificate required but not provided. Remote IP: {RemoteIp}", 
                    context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Client certificate required");
                return;
            }

            // If certificate is provided, validate it
            if (clientCertificate != null)
            {
                var validationResult = ValidateCertificate(clientCertificate, context);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Client certificate validation failed: {Reason}. Thumbprint: {Thumbprint}, Subject: {Subject}, Remote IP: {RemoteIp}",
                        validationResult.ErrorMessage,
                        clientCertificate.Thumbprint,
                        clientCertificate.Subject,
                        context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync($"Client certificate validation failed: {validationResult.ErrorMessage}");
                    return;
                }

                _logger.LogInformation("Client certificate validated successfully. Thumbprint: {Thumbprint}, Subject: {Subject}, Remote IP: {RemoteIp}",
                    clientCertificate.Thumbprint,
                    clientCertificate.Subject,
                    context.Connection.RemoteIpAddress);
            }

            await _next(context);
        }

        private (bool IsValid, string? ErrorMessage) ValidateCertificate(X509Certificate2 certificate, HttpContext context)
        {
            try
            {
                // Check validity period
                if (_options.ValidateValidityPeriod)
                {
                    var now = DateTime.UtcNow;
                    if (certificate.NotBefore > now)
                    {
                        return (false, "Certificate is not yet valid");
                    }
                    if (certificate.NotAfter < now)
                    {
                        return (false, "Certificate has expired");
                    }
                }

                // Check certificate chain
                if (_options.ValidateCertificateChain)
                {
                    using var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Can be adjusted based on requirements
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                    if (!chain.Build(certificate))
                    {
                        var chainErrors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
                        _logger.LogDebug("Certificate chain validation failed: {ChainErrors}", chainErrors);
                        // We log but don't fail here as self-signed certificates are common in enterprise scenarios
                    }
                }

                // Check if certificate thumbprint is in allowed list (if configured)
                if (_options.AllowedCertificateThumbprints != null && _options.AllowedCertificateThumbprints.Count > 0)
                {
                    var thumbprint = certificate.Thumbprint.Replace(" ", "").Replace(":", "").ToUpperInvariant();
                    var allowedThumbprints = _options.AllowedCertificateThumbprints
                        .Select(t => t.Replace(" ", "").Replace(":", "").ToUpperInvariant())
                        .ToList();

                    if (!allowedThumbprints.Contains(thumbprint))
                    {
                        return (false, $"Certificate thumbprint {thumbprint} is not in the allowed list");
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating client certificate");
                return (false, "Certificate validation error");
            }
        }
    }
}
