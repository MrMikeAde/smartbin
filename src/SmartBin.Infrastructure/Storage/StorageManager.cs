using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Storage
{
    public class StorageManager : IStorageManager
    {
        private readonly IStoragePathProvider _pathProvider;
        private readonly string _objectsDir;
        private readonly string _tempDir;
        private readonly string _metadataDir;

        public StorageManager(IStoragePathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));

            var root = _pathProvider.GetRootPath();
            _objectsDir = Path.Combine(root, "objects");
            _tempDir = Path.Combine(root, "temp");
            _metadataDir = Path.Combine(root, "metadata");

            // Safe initialization
            InitializeDirectories();
        }

        private void InitializeDirectories()
        {
            Directory.CreateDirectory(_objectsDir);
            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_metadataDir);
        }

        public string GetStoragePath()
        {
            return _pathProvider.GetRootPath();
        }

        public string ObjectsDirectory => _objectsDir;
        public string TempDirectory => _tempDir;
        public string MetadataDirectory => _metadataDir;

        public async Task<string> MoveToStorageAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source file does not exist.", sourcePath);
            }

            // Create a safe, unique destination file name to avoid collisions
            var fileId = Guid.NewGuid().ToString("N");
            var destinationPath = Path.Combine(_objectsDir, fileId);

            // Copy file safely with async streams (large-file handling)
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var destStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream, cancellationToken);
            }

            return destinationPath;
        }

        public Task<long> GetAvailableFreeSpaceAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var root = _pathProvider.GetRootPath();
                var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root)) ?? "/");
                return Task.FromResult(driveInfo.AvailableFreeSpace);
            }
            catch
            {
                // Fallback to a default size if path resolution/permissions fail on non-Windows/CI environment
                return Task.FromResult(100 * 1024 * 1024 * 1024L); // 100 GB
            }
        }
    }
}
