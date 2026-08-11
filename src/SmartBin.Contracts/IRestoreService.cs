using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IRestoreService
    {
        /// <summary>
        /// Restores a deleted item back to its original or specified path, verifying integrity.
        /// </summary>
        Task RestoreAsync(Guid itemId, string? targetPath = null, CancellationToken cancellationToken = default);
    }
}
