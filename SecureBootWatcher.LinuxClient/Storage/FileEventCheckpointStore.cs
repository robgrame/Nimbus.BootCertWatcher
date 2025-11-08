using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SecureBootWatcher.LinuxClient.Storage
{
    internal sealed class FileEventCheckpointStore : IEventCheckpointStore
    {
        private readonly string _checkpointFilePath;

        public FileEventCheckpointStore()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SecureBootWatcher");
            Directory.CreateDirectory(root);
            _checkpointFilePath = Path.Combine(root, "event-checkpoint.txt");
        }

        public async Task<DateTimeOffset?> GetLastCheckpointAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_checkpointFilePath))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(_checkpointFilePath, cancellationToken);
            if (DateTimeOffset.TryParseExact(content, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        public async Task SetCheckpointAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(_checkpointFilePath, timestampUtc.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
        }
    }
}
