using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Components
{
    public class TimingRegion
    {
        private readonly static Stack<TimingRegion> _currentRegionsStack = new Stack<TimingRegion>();
        private DateTime? _currentlyActiveStartTime;

        public string Name { get; }
        public Dictionary<string, TimingRegion> Children { get; } = new Dictionary<string, TimingRegion>();
        public int TotalCount { get; private set; }
        public double TotalDuration { get; private set; }

        private TimingRegion(string name)
        {
            Name = name;
        }

        public static TimingRegion Open(string name)
        {
            var result = _currentRegionsStack.Count > 0
                ? _currentRegionsStack.Peek().GetOrCreateChild(name)
                : new TimingRegion(name);
            if (result._currentlyActiveStartTime.HasValue)
            {
                throw new InvalidOperationException($"Trying to start timing region { result.Name } when it is already running.");
            }

            _currentRegionsStack.Push(result);

            result._currentlyActiveStartTime = DateTime.Now;
            result.TotalCount++;

            return result;
        }

        public void Close()
        {
            var endTime = DateTime.Now;
            if (!_currentlyActiveStartTime.HasValue)
            {
                throw new InvalidOperationException($"Trying to stop timing region { Name } when it is not already running.");
            }

            var duration = endTime - _currentlyActiveStartTime.Value;
            TotalDuration += duration.TotalMilliseconds;
            _currentlyActiveStartTime = null;

            var poppedInstance = _currentRegionsStack.Pop();
            if (poppedInstance != this)
            {
                throw new InvalidOperationException($"Timing region close mismatch. Trying to close { Name }, but the top instance on the stack is { poppedInstance.Name }.");
            }
        }

        private TimingRegion GetOrCreateChild(string name)
        {
            if (Children.TryGetValue(name, out var existingChild))
            {
                return existingChild;
            }
            else
            {
                var newChild = new TimingRegion(name);
                Children.Add(name, newChild);
                return newChild;
            }
        }
    }
}
