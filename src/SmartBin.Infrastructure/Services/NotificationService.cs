using System;
using System.Collections.Generic;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        public event Action<string, string>? NotificationRaised; // Message, Type

        private readonly Dictionary<string, DateTime> _lastNotificationTimes = new();
        private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(5); // Cooldown throttling

        public void RaiseNotification(string message, string type)
        {
            var key = type + "_" + message;
            var now = DateTime.UtcNow;

            if (_lastNotificationTimes.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < _cooldown)
                {
                    return; // Throttle / Suppress duplicate notification
                }
            }

            _lastNotificationTimes[key] = now;
            NotificationRaised?.Invoke(message, type);
        }
    }
}
