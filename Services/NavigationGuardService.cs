using System;
using System.Collections.Generic;
using System.Linq;

namespace GuaranteeManager.Services
{
    public sealed class NavigationGuardService : INavigationGuard
    {
        private readonly object _gate = new();
        private readonly List<string> _blockingReasons = new();

        public bool CanNavigateAway(out string blockingReason)
        {
            lock (_gate)
            {
                blockingReason = _blockingReasons.FirstOrDefault() ?? string.Empty;
                return _blockingReasons.Count == 0;
            }
        }

        public IDisposable Block(string blockingReason)
        {
            string normalizedReason = string.IsNullOrWhiteSpace(blockingReason)
                ? "توجد عملية جارية تمنع التنقل الآن."
                : blockingReason.Trim();

            lock (_gate)
            {
                _blockingReasons.Add(normalizedReason);
            }

            return new ReleaseScope(this, normalizedReason);
        }

        private void Release(string blockingReason)
        {
            lock (_gate)
            {
                _blockingReasons.Remove(blockingReason);
            }
        }

        private sealed class ReleaseScope : IDisposable
        {
            private readonly NavigationGuardService _owner;
            private readonly string _blockingReason;
            private bool _disposed;

            public ReleaseScope(NavigationGuardService owner, string blockingReason)
            {
                _owner = owner;
                _blockingReason = blockingReason;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.Release(_blockingReason);
            }
        }
    }
}
