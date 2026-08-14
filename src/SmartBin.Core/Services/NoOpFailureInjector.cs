using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class NoOpFailureInjector : IFailureInjector
    {
        public void Check(string checkpoint)
        {
            // Production no-op: does absolutely nothing
        }
    }
}
