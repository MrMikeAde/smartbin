using System;
using System.Runtime.InteropServices;

namespace SmartBin.App
{
    /// <summary>
    /// Program class to bootstrap the application.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("SmartBin UI Application Starting...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunWindowsApp();
            }
            else
            {
                RunMockDashboard();
            }
        }

        // Keep this method isolated so that JIT compiler doesn't fail on non-Windows platforms
        // where Microsoft.UI.Xaml might not be present at runtime.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RunWindowsApp()
        {
#if WINDOWS
            Microsoft.UI.Xaml.Application.Start((p) => new App());
#else
            RunMockDashboard();
#endif
        }

        private static void RunMockDashboard()
        {
            Console.WriteLine("Mocking dashboard presentation of SmartBin:");
            Console.WriteLine("=========================================");
            Console.WriteLine("Storage: Visualized 45% filled");
            Console.WriteLine("Recoverable Items: 15");
            Console.WriteLine("Original Storage Size: 120 GB");
            Console.WriteLine("Stored Size: 42 GB");
            Console.WriteLine("Space Reclaimed: 78 GB");
            Console.WriteLine("Recent Items: None");
            Console.WriteLine("=========================================");
        }
    }
}
