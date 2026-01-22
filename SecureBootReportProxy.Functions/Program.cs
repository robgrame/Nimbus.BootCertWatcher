using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureBootReportProxy.Functions.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register configuration from environment variables
        services.Configure<ProxyFunctionOptions>(options =>
        {
            var config = context.Configuration;
            options.ApiKey = config["ApiKey"] ?? string.Empty;
            options.QueueStorageUri = config["QueueStorageUri"] ?? string.Empty;
            options.QueueName = config["QueueName"] ?? "secureboot-reports";
            options.RequireCertificateAuthentication = bool.Parse(config["RequireCertificateAuthentication"] ?? "false");
            
            // Basic certificate validation settings
            options.CertificateAuthentication.AllowedThumbprints = config["CertificateThumbprints"] ?? string.Empty;
            options.CertificateAuthentication.ValidateExpiration = bool.Parse(config["CertificateValidateExpiration"] ?? "true");
            options.CertificateAuthentication.ValidateCertificateChain = bool.Parse(config["CertificateValidateChain"] ?? "true");
            options.CertificateAuthentication.CheckCertificateRevocation = bool.Parse(config["CertificateCheckRevocation"] ?? "false");
            
            // Root CA validation settings
            options.CertificateAuthentication.ExpectedCARootName = config["CertificateExpectedCARootName"];
            options.CertificateAuthentication.ExpectedCARootThumbprint = config["CertificateExpectedCARootThumbprint"];
            
            // Subordinate CA validation settings
            options.CertificateAuthentication.ExpectedSubordinateCAsJson = config["CertificateExpectedSubordinateCAsJson"];
        });
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConsole();
        logging.AddApplicationInsights();
    })
    .Build();

host.Run();
