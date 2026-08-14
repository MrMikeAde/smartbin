using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Services;

namespace SmartBin.Infrastructure.Persistence
{
    public class ActivityRepository : IActivityLogger
    {
        private readonly SmartBinDbContext _dbContext;
        private readonly IFailureInjector _failureInjector;

        public ActivityRepository(SmartBinDbContext dbContext, IFailureInjector? failureInjector = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _failureInjector = failureInjector ?? new NoOpFailureInjector();
        }

        public async Task AddLogAsync(ActivityLog log, CancellationToken cancellationToken = default)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            _failureInjector.Check("BeforeActivityPersistence");
            await _dbContext.ActivityLogs.AddAsync(log, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _failureInjector.Check("AfterActivityPersistence");
        }

        public async Task<List<ActivityLog>> GetLogsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync(cancellationToken);
        }
    }
}
