using System;
using System.IO;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Storage
{
    public class DefaultStoragePathProvider : IStoragePathProvider
    {
        private readonly string _customRootPath;

        public DefaultStoragePathProvider(string? customRootPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customRootPath))
            {
                _customRootPath = customRootPath;
            }
            else
            {
                // Sensible default locations depending on the OS platform
                string appData;
                if (OperatingSystem.IsWindows())
                {
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _customRootPath = Path.Combine(appData, "SmartBinStorage");
                }
                else
                {
                    appData = Path.GetTempPath();
                    _customRootPath = Path.Combine(appData, "SmartBinStorage_" + Guid.NewGuid().ToString("N"));
                }
            }
        }

        public string GetRootPath()
        {
            return _customRootPath;
        }
    }
}
