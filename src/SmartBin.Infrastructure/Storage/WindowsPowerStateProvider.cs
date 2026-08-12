using System;
using System.Runtime.InteropServices;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Storage
{
    public class WindowsPowerStateProvider : IPowerStateProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

        public bool IsOnBatteryPower()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Headless mock: default to false (pretend we are on AC power)
                return false;
            }

            try
            {
                if (GetSystemPowerStatus(out var status))
                {
                    // ACLineStatus: 0 = Offline (Battery), 1 = Online (AC), 255 = Unknown
                    return status.ACLineStatus == 0;
                }
            }
            catch
            {
                // Fallback on error
            }

            return false;
        }
    }
}
