using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IStorageManager
    {
        /// <summary>
        /// Gets the base path of the controlled SmartBin storage.
        /// </summary>
        string GetStoragePath();

        /// <summary>
        /// Moves a file into the controlled storage area.
        /// </summary>
        Task<string> MoveToStorageAsync(string sourcePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks available space in the storage volume.
        /// </summary>
        Task<long> GetAvailableFreeSpaceAsync(CancellationToken cancellationToken = default);
    }
}
