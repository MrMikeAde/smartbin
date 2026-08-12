using System;

namespace SmartBin.Contracts
{
    public interface INotificationService
    {
        void RaiseNotification(string message, string type);
        event Action<string, string>? NotificationRaised;
    }
}
