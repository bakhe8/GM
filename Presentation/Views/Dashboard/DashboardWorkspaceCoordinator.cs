using System;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class DashboardWorkspaceCoordinator
    {
        public void RunPrimaryAction(
            DashboardWorkItem? item,
            Action<int, GuaranteeFocusArea, int?> openGuaranteeContext,
            Action showGuarantees)
        {
            if (item == null)
            {
                return;
            }

            if (item.RootGuaranteeId > 0)
            {
                openGuaranteeContext(item.RootGuaranteeId, item.PrimaryFocusArea, item.RequestId);
                return;
            }

            showGuarantees();
        }
    }
}
