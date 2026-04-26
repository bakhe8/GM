using System;
using System.Windows;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class DashboardWorkspaceCoordinator
    {
        public void CopyReference(DashboardWorkItem? item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(item.Reference);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "نسخ المرجع", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void OpenSelectedWorkspace(
            DashboardWorkItem? item,
            Action showGuarantees,
            Action showRequests,
            Action showNotifications,
            Action showReports)
        {
            if (item == null)
            {
                return;
            }

            switch (item.Target)
            {
                case DashboardTarget.Requests:
                    showRequests();
                    break;
                case DashboardTarget.Notifications:
                    showNotifications();
                    break;
                case DashboardTarget.Reports:
                    showReports();
                    break;
                default:
                    showGuarantees();
                    break;
            }
        }
    }
}
