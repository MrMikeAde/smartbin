using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Core.Services
{
    public static class TestFileGenerator
    {
        /// <summary>
        /// Generates custom deterministic test files for experimental SmartBin testing.
        /// </summary>
        public static async Task GenerateTestFileAsync(string targetPath, string type, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) throw new ArgumentException("Target path cannot be null or empty.", nameof(targetPath));

            var folder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            switch (type.ToLowerInvariant())
            {
                case "10mb_compressible":
                    // 10 MB highly compressible text repeating
                    var chunk10 = Encoding.UTF8.GetBytes("SmartBin Highly Compressible Proof File! " + new string('X', 4000) + "\n");
                    for (int i = 0; i < 2500; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        await stream.WriteAsync(chunk10, cancellationToken);
                    }
                    break;

                case "100mb_compressible":
                    // 100 MB highly compressible text repeating
                    var chunk100 = Encoding.UTF8.GetBytes("SmartBin Large 100MB Compressible File Content! " + new string('Y', 8000) + "\n");
                    for (int i = 0; i < 13000; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        await stream.WriteAsync(chunk100, cancellationToken);
                    }
                    break;

                case "500mb_mixed":
                    // 500 MB mixed data
                    var textData = Encoding.UTF8.GetBytes("SmartBin Mixed Data " + new string('Z', 5000));
                    var randomData = new byte[5000];
                    Random.Shared.NextBytes(randomData);

                    for (int i = 0; i < 50000; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        await stream.WriteAsync(textData, cancellationToken);
                        await stream.WriteAsync(randomData, cancellationToken);
                    }
                    break;

                case "1gb_incompressible":
                    // 1 GB random incompressible bytes
                    var randomChunk = new byte[1024 * 1024]; // 1 MB chunk
                    for (int i = 0; i < 1024; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        Random.Shared.NextBytes(randomChunk);
                        await stream.WriteAsync(randomChunk, cancellationToken);
                    }
                    break;

                default:
                    throw new ArgumentException($"Unknown test file type: {type}", nameof(type));
            }
        }
    }
}
