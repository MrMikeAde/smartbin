using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IFileHasher
    {
        /// <summary>
        /// Computes the SHA-256 hash of a file as a hex string.
        /// </summary>
        Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Computes the SHA-256 hash of a stream as a hex string.
        /// </summary>
        Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default);
    }
}
