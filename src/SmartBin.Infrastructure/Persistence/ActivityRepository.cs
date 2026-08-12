using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Persistence
{
    public class ActivityRepository : IActivityLogger
    {
        private readonly SmartBinDbContext _dbContext;

        public ActivityRepository(SmartBinDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task AddLogAsync(ActivityLog log, CancellationToken cancellationToken = default)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            await _dbContext.ActivityLogs.AddAsync(log, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<ActivityLog>> GetLogsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync(cancellationToken);
        }
    }
}
