using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Compression
{
    public class ZipCompressionService : ICompressionService
    {
        /// <summary>
        /// Analyzes compressibility by looking at the first 128KB of the file.
        /// Performs a lightweight mock/sample deflate run to estimate the ratio.
        /// </summary>
        public async Task<long> AnalyzeAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("File not found for analysis.", sourcePath);
            }

            var fileInfo = new FileInfo(sourcePath);
            var originalSize = fileInfo.Length;

            if (originalSize == 0) return 0;

            // Sample up to 128KB of data
            var sampleSize = (int)Math.Min(128 * 1024, originalSize);
            var buffer = new byte[sampleSize];

            using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                await fs.ReadExactlyAsync(buffer, 0, sampleSize, cancellationToken);
            }

            // Estimate compression by deflating the sample
            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await deflateStream.WriteAsync(buffer, 0, sampleSize, cancellationToken);
                }

                var compressedSampleSize = memoryStream.Length;
                double ratio = (double)compressedSampleSize / sampleSize;

                // Return estimated size
                return (long)Math.Max(1, originalSize * ratio);
            }
        }

        /// <summary>
        /// Compresses a source file to a target file using stream-based DeflateStream.
        /// It prepends the original file length as an 8-byte header to make decompression safe and transparent.
        /// </summary>
        public async Task CompressAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Target path cannot be null or empty.", nameof(targetPath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found for compression.", sourcePath);

            var fileInfo = new FileInfo(sourcePath);
            var originalSize = fileInfo.Length;

            // Stream-based compression supporting large files
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var destStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                // Write original file size as header
                var header = BitConverter.GetBytes(originalSize);
                await destStream.WriteAsync(header, cancellationToken);

                using (var deflateStream = new DeflateStream(destStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await sourceStream.CopyToAsync(deflateStream, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Decompresses a deflate archive back to the original file path.
        /// </summary>
        public async Task DecompressAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Archive path cannot be null or empty.", nameof(archivePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationPath));

            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Archive file not found for decompression.", archivePath);

            using (var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                // Read original file size header (8 bytes)
                var header = new byte[8];
                await archiveStream.ReadExactlyAsync(header, 0, 8, cancellationToken);

                using (var deflateStream = new DeflateStream(archiveStream, CompressionMode.Decompress, leaveOpen: true))
                {
                    await deflateStream.CopyToAsync(destStream, cancellationToken);
                }
            }
        }
    }
}
