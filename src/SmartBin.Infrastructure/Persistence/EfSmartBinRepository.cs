using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Infrastructure.Persistence
{
    public class EfSmartBinRepository : ISmartBinRepository<SmartBinItem>
    {
        private readonly SmartBinDbContext _dbContext;

        public EfSmartBinRepository(SmartBinDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<SmartBinItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.SmartBinItems
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<SmartBinItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SmartBinItems.ToListAsync(cancellationToken);
        }

        public async Task AddAsync(SmartBinItem item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            await _dbContext.SmartBinItems.AddAsync(item, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(SmartBinItem item, CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _dbContext.SmartBinItems.Update(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = await GetByIdAsync(id, cancellationToken);
            if (item != null)
            {
                _dbContext.SmartBinItems.Remove(item);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
