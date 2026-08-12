using System;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Storage
{
    public class StorageMonitor : IStorageMonitor, IDisposable
    {
        private readonly IStoragePressureMonitor _pressureMonitor;
        private CancellationTokenSource? _cts;
        private StoragePressureState? _lastKnownState;
        private bool _isMonitoringActive;

        public event Action<StorageSpaceMetrics>? PressureStateChanged;

        public StorageMonitor(IStoragePressureMonitor pressureMonitor)
        {
            _pressureMonitor = pressureMonitor ?? throw new ArgumentNullException(nameof(pressureMonitor));
        }

        public bool IsMonitoringActive => _isMonitoringActive;

        public void StartMonitoring(TimeSpan interval)
        {
            if (_isMonitoringActive) return;

            _cts = new CancellationTokenSource();
            _isMonitoringActive = true;

            Task.Run(async () =>
            {
                using var periodicTimer = new PeriodicTimer(interval);
                while (_isMonitoringActive && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var metrics = await _pressureMonitor.GetStorageMetricsAsync(_cts.Token);

                        if (_lastKnownState == null || _lastKnownState.Value != metrics.PressureState)
                        {
                            _lastKnownState = metrics.PressureState;
                            PressureStateChanged?.Invoke(metrics);
                        }
                    }
                    catch
                    {
                        // Safely ignore and continue on next tick
                    }

                    // Await next tick
                    try
                    {
                        await periodicTimer.WaitForNextTickAsync(_cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        public void StopMonitoring()
        {
            if (!_isMonitoringActive) return;

            _isMonitoringActive = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
