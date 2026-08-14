using System;
using System.Collections.Generic;
using SmartBin.Contracts;

namespace SmartBin.Infrastructure.Tests.Helpers
{
    public class TestFailureInjector : IFailureInjector
    {
        private readonly HashSet<string> _activeCheckpoints = new(StringComparer.OrdinalIgnoreCase);

        public void Enable(string checkpoint)
        {
            _activeCheckpoints.Add(checkpoint);
        }

        public void Disable(string checkpoint)
        {
            _activeCheckpoints.Remove(checkpoint);
        }

        public void Clear()
        {
            _activeCheckpoints.Clear();
        }

        public void Check(string checkpoint)
        {
            if (_activeCheckpoints.Contains(checkpoint))
            {
                throw new FailureInjectionException(checkpoint, $"Injected failure at checkpoint: {checkpoint}");
            }
        }
    }
}
