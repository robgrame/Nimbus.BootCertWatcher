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

        public Task<DateTimeOffset?> GetLastCheckpointAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_checkpointFilePath))
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var content = File.ReadAllText(_checkpointFilePath);
            if (DateTimeOffset.TryParseExact(content, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return Task.FromResult<DateTimeOffset?>(parsed);
            }

            return Task.FromResult<DateTimeOffset?>(null);
        }

        public Task SetCheckpointAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(_checkpointFilePath, timestampUtc.ToString("O", CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }
    }
}
