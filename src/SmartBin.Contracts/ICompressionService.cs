using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface ICompressionService
    {
        /// <summary>
        /// Analyzes a file to estimate its compressibility.
        /// Returns an expected compressed size or compression ratio.
        /// </summary>
        Task<long> AnalyzeAsync(string sourcePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Losslessly compresses a file to a target archive file.
        /// </summary>
        Task CompressAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decompresses an archive back to the original file path.
        /// </summary>
        Task DecompressAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default);
    }
}
