using System;

namespace SmartBin.Contracts
{
    public class SmartBinException : Exception
    {
        public SmartBinException(string message) : base(message)
        {
        }

        public SmartBinException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class SmartBinConflictException : SmartBinException
    {
        public string ConflictingPath { get; }

        public SmartBinConflictException(string message, string conflictingPath) : base(message)
        {
            ConflictingPath = conflictingPath;
        }
    }
}
