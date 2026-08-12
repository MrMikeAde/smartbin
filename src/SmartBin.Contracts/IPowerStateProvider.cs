namespace SmartBin.Contracts
{
    public interface IPowerStateProvider
    {
        /// <summary>
        /// Returns true if the system is running on battery power.
        /// </summary>
        bool IsOnBatteryPower();
    }
}
