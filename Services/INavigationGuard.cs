using System;

namespace GuaranteeManager.Services
{
    public interface INavigationGuard
    {
        bool CanNavigateAway(out string blockingReason);

        IDisposable Block(string blockingReason);
    }
}
