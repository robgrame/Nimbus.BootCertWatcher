using Azure.Storage.Blobs;

namespace WindowsVersionsCore.Services.BackgroundTasks
{
    /// <summary>
    /// Background service that initializes required Azure Storage containers
    /// </summary>
    public class StorageContainerInitializer : BackgroundService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<StorageContainerInitializer> _logger;

        public StorageContainerInitializer(
            BlobServiceClient blobServiceClient,
            ILogger<StorageContainerInitializer> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Initializing Azure Storage containers");

                // Create the main container for Windows versions data
                var containerClient = _blobServiceClient.GetBlobContainerClient("windowsversions");
                await containerClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

                _logger.LogInformation("Storage container initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize storage containers");
                throw; // Rethrow to signal initialization failure
            }
        }
    }
}