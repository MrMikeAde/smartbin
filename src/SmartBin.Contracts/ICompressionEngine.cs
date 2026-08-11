using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface ICompressionEngine
    {
        /// <summary>
        /// Process an uncompressed SmartBin item through the atomic compression pipeline.
        /// If compression is beneficial and successfully verified, replaces original stored representation.
        /// </summary>
        Task CompressItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    }
}
