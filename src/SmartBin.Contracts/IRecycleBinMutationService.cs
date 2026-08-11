using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IRecycleBinMutationService
    {
        /// <summary>
        /// Safely extracts/copies the deleted item's content from the Recycle Bin to a temporary path.
        /// </summary>
        Task ExtractItemContentAsync(string itemId, string targetTempPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes/permanently deletes the item from the Windows Recycle Bin.
        /// This is a mutating operation and must only be executed under strict verified conditions.
        /// </summary>
        Task RemoveItemAsync(string itemId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores the item to its original path using the native Windows Shell.
        /// </summary>
        Task RestoreItemAsync(string itemId, CancellationToken cancellationToken = default);
    }
}
