using System;

namespace SmartBin.Contracts
{
    public class FailureInjectionException : Exception
    {
        public string Checkpoint { get; }

        public FailureInjectionException(string checkpoint, string message) : base(message)
        {
            Checkpoint = checkpoint;
        }
    }

    public interface IFailureInjector
    {
        /// <summary>
        /// Checks if a failure should be injected at the specified named checkpoint.
        /// Throws a FailureInjectionException if a failure is configured/triggered.
        /// </summary>
        void Check(string checkpoint);
    }
}
