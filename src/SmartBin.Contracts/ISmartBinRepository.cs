using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    // A placeholder for SmartBinItem metadata inside Contracts to avoid circular dependencies if needed,
    // or we can use a generic parameter. Since Core maps directly to Contracts, we can make this interface generic or use a DTO.
    // Let's make it generic over TItem to keep contracts pure and decouple it from domain models if needed,
    // or just let it use the core models by referencing Core inside Contracts, but the prompt's reference diagram is:
    // SmartBin.Core -> SmartBin.Contracts
    // SmartBin.Infrastructure -> SmartBin.Core -> SmartBin.Contracts
    // SmartBin.App -> SmartBin.Core -> SmartBin.Contracts
    //
    // Thus Contracts is the lowest layer! It cannot depend on Core.
    // So the repository interface in Contracts must either use generic models or basic data transfer values,
    // or we can use an interface `ISmartBinItem` defined in Contracts which the Core model implements.
    // Let's define ISmartBinItem inside Contracts! This is extremely clean.

    public interface ISmartBinItem
    {
        Guid Id { get; }
        string OriginalPath { get; }
        string OriginalFileName { get; }
        string OriginalExtension { get; }
        long OriginalSize { get; }
        DateTime DeletedTimestamp { get; }
        DateTime? OriginalCreationTimestamp { get; }
        DateTime? OriginalModificationTimestamp { get; }
        string Sha256Hash { get; }
        string CurrentStoragePath { get; }
        long CurrentStoredSize { get; }
        int CompressionStatus { get; } // Map to int or string to avoid hardcoding Core enums
        int CompressionAlgorithm { get; }
        DateTime? CompressionTimestamp { get; }
        int RestorationStatus { get; }
    }

    public interface ISmartBinRepository<TItem> where TItem : class, ISmartBinItem
    {
        Task<TItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<TItem>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(TItem item, CancellationToken cancellationToken = default);
        Task UpdateAsync(TItem item, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
