using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Storage
{
    public class WindowsRecycleBinProvider : IRecycleBinProvider
    {
        public Task<IEnumerable<WindowsRecycleBinItem>> EnumerateItemsAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<WindowsRecycleBinItem>();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Safe read-only fallback on non-Windows platforms
                return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);
            }

            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);

                // Namespace 10 corresponds to ssfBITBUCKET (virtual Recycle Bin)
                dynamic? recycleBin = shell.NameSpace(10);
                if (recycleBin == null) return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);

                dynamic? items = recycleBin.Items();
                if (items == null) return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);

                int count = items.Count;
                for (int i = 0; i < count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        dynamic? item = items.Item(i);
                        if (item == null) continue;

                        string name = item.Name ?? "";
                        string path = item.Path ?? "";

                        // Retrieve metadata via GetDetailsOf columns:
                        // Column 1 is typically Original Location (Original Path)
                        // Column 2 is typically Date Deleted
                        // Column 3 is typically File Size
                        string origLocation = recycleBin.GetDetailsOf(item, 1) ?? "";
                        string dateDeletedStr = recycleBin.GetDetailsOf(item, 2) ?? "";
                        string sizeStr = recycleBin.GetDetailsOf(item, 3) ?? "";

                        long itemSize = 0;
                        try
                        {
                            itemSize = item.Size; // Try retrieving raw byte size from shell item
                        }
                        catch
                        {
                            // Fallback to parsing size column string (e.g. "4.20 KB" or "10,240 bytes")
                            itemSize = ParseSizeString(sizeStr);
                        }

                        DateTime? deletedTime = null;
                        if (DateTime.TryParse(dateDeletedStr, out var parsedDate))
                        {
                            deletedTime = parsedDate;
                        }

                        // Determine volume from original path or fallback
                        string volume = "C:";
                        if (!string.IsNullOrWhiteSpace(origLocation) && origLocation.Contains(":"))
                        {
                            volume = origLocation.Split(':')[0] + ":";
                        }
                        else if (!string.IsNullOrWhiteSpace(path) && path.Contains(":"))
                        {
                            volume = path.Split(':')[0] + ":";
                        }

                        var binItem = new WindowsRecycleBinItem
                        {
                            Id = "win_" + i.ToString(),
                            FileName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(origLocation) : name,
                            OriginalPath = string.IsNullOrWhiteSpace(origLocation) ? path : origLocation,
                            Size = itemSize,
                            DeletedTimestamp = deletedTime,
                            Volume = volume,
                            IsSimulated = false
                        };

                        results.Add(binItem);
                    }
                    catch
                    {
                        // Safely skip any corrupted/inaccessible Recycle Bin item
                    }
                }
            }
            catch
            {
                // Safely log or handle Recycle Bin api inaccessible
            }

            return Task.FromResult<IEnumerable<WindowsRecycleBinItem>>(results);
        }

        public async Task<WindowsRecycleBinItem?> GetItemAsync(string id, CancellationToken cancellationToken = default)
        {
            var items = await EnumerateItemsAsync(cancellationToken);
            foreach (var item in items)
            {
                if (item.Id == id) return item;
            }
            return null;
        }

        public async Task<RecycleBinStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var items = await EnumerateItemsAsync(cancellationToken);
            var stats = new RecycleBinStatistics();
            foreach (var item in items)
            {
                stats.TotalItems++;
                stats.TotalSize += item.Size;
            }
            return stats;
        }

        private static long ParseSizeString(string sizeStr)
        {
            if (string.IsNullOrWhiteSpace(sizeStr)) return 0;

            try
            {
                // Remove commas/spaces and strip non-digit characters to estimate raw bytes
                var clean = new System.Text.StringBuilder();
                foreach (var c in sizeStr)
                {
                    if (char.IsDigit(c)) clean.Append(c);
                }

                if (clean.Length > 0 && long.TryParse(clean.ToString(), out var bytes))
                {
                    // If the original string contains "KB" or "MB" or "GB", scale accordingly
                    var upper = sizeStr.ToUpperInvariant();
                    if (upper.Contains("GB")) return bytes * 1024 * 1024 * 1024;
                    if (upper.Contains("MB")) return bytes * 1024 * 1024;
                    if (upper.Contains("KB")) return bytes * 1024;
                    return bytes;
                }
            }
            catch { }

            return 0;
        }
    }
}
