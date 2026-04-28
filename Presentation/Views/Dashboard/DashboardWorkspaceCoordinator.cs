using System;
using System.Windows;
using GuaranteeManager.Models;
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
            Action<string?> showRequests,
            Action<string?> showNotifications,
            Action<string?> showReports)
        {
            if (item == null)
            {
                return;
            }

            string? initialSearchText = ResolveWorkspaceContext(item);

            switch (item.Target)
            {
                case DashboardTarget.Requests:
                    showRequests(initialSearchText);
                    break;
                case DashboardTarget.Notifications:
                    showNotifications(initialSearchText);
                    break;
                case DashboardTarget.Reports:
                    showReports(initialSearchText);
                    break;
                default:
                    showGuarantees();
                    break;
            }
        }

        public void RunPrimaryAction(
            DashboardWorkItem? item,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
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

        private static string? ResolveWorkspaceContext(DashboardWorkItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Reference))
            {
                return item.Reference.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                return item.Title.Trim();
            }

            return null;
        }
    }
}
