using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IRestoreService
    {
        /// <summary>
        /// Restores a deleted item back to its original path, verifying integrity.
        /// </summary>
        Task RestoreAsync(Guid itemId, CancellationToken cancellationToken = default);
    }
}
