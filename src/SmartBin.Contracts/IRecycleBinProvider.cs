using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IRecycleBinProvider
    {
        /// <summary>
        /// Enumerates items inside the Windows Recycle Bin.
        /// </summary>
        Task<IEnumerable<WindowsRecycleBinItem>> EnumerateItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets details for a specific Windows Recycle Bin item.
        /// </summary>
        Task<WindowsRecycleBinItem?> GetItemAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets aggregate statistics of the Windows Recycle Bin.
        /// </summary>
        Task<RecycleBinStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }
}
