using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class SimulatedRecycleBinProvider : IRecycleBinProvider
    {
        private readonly List<WindowsRecycleBinItem> _items;

        public SimulatedRecycleBinProvider()
        {
            _items = new List<WindowsRecycleBinItem>
            {
                new()
                {
                    Id = "sim_1",
                    FileName = "database.sql",
                    OriginalPath = @"C:\Users\User\Documents\database.sql",
                    Size = 4L * 1024 * 1024 * 1024 + 200L * 1024 * 1024, // 4.2 GB
                    DeletedTimestamp = DateTime.UtcNow.AddDays(-42),
                    Volume = "C:",
                    IsSimulated = true
                },
                new()
                {
                    Id = "sim_2",
                    FileName = "logs.txt",
                    OriginalPath = @"C:\Users\User\Logs\logs.txt",
                    Size = 920L * 1024 * 1024, // 920 MB
                    DeletedTimestamp = DateTime.UtcNow.AddDays(-90),
                    Volume = "C:",
                    IsSimulated = true
                },
                new()
                {
                    Id = "sim_3",
                    FileName = "video.mp4",
                    OriginalPath = @"D:\Media\video.mp4",
                    Size = 1L * 1024 * 1024 * 1024 + 700L * 1024 * 1024, // 1.7 GB
                    DeletedTimestamp = DateTime.UtcNow.AddDays(-3),
                    Volume = "D:",
                    IsSimulated = true
                },
                new()
                {
                    Id = "sim_4",
                    FileName = "photo.jpg",
                    OriginalPath = @"E:\Backup\photo.jpg",
                    Size = 150L * 1024 * 1024, // 150 MB
                    DeletedTimestamp = DateTime.UtcNow.AddDays(-10),
                    Volume = "E:",
                    IsSimulated = true
                }
            };
        }

        public Task<IEnumerable<WindowsRecycleBinItem>> EnumerateItemsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(_items);
        }

        public Task<WindowsRecycleBinItem?> GetItemAsync(string id, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            return Task.FromResult(item);
        }

        public Task<RecycleBinStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stats = new RecycleBinStatistics
            {
                TotalItems = _items.Count,
                TotalSize = _items.Sum(i => i.Size)
            };
            return Task.FromResult(stats);
        }
    }
}
