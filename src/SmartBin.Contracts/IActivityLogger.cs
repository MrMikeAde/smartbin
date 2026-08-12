using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IActivityLogger
    {
        Task AddLogAsync(ActivityLog log, CancellationToken cancellationToken = default);
        Task<List<ActivityLog>> GetLogsAsync(CancellationToken cancellationToken = default);
    }
}
