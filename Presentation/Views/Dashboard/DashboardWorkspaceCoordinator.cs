using System;
using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class DashboardWorkspaceCoordinator
    {
        private readonly IShellStatusService _shellStatus;

        public DashboardWorkspaceCoordinator()
        {
            _shellStatus = App.CurrentApp.GetRequiredService<IShellStatusService>();
        }

        public void CopyReference(DashboardWorkItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Reference, "المرجع", BuildStatusContext(item));
        }

        public void CopyBank(DashboardWorkItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Bank, "البنك", BuildStatusContext(item));
        }

        public void CopyAmount(DashboardWorkItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.AmountDisplay, "القيمة", BuildStatusContext(item));
        }

        private void CopyText(string value, string label, string secondaryText)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "---")
            {
                MessageBox.Show($"لا توجد قيمة متاحة لنسخ {label}.", $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(value);
                _shellStatus.ShowInfo($"تم نسخ {label}.", secondaryText);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string BuildStatusContext(DashboardWorkItem item)
        {
            string reference = string.IsNullOrWhiteSpace(item.Reference) ? "عنصر محدد" : item.Reference;
            return $"اليوم • {reference}";
        }

        public void OpenSelectedWorkspace(
            DashboardWorkItem? item,
            Action<string?, string?> showToday,
            Action showGuarantees,
            Action<string?, int?> showRequests,
            Action<string?> showReports)
        {
            if (item == null)
            {
                return;
            }

            string? initialSearchText = ResolveWorkspaceContext(item);

            switch (item.Target)
            {
                case DashboardTarget.Today:
                    showToday(initialSearchText, ResolveTodayScope(item));
                    break;
                case DashboardTarget.Requests:
                    showRequests(initialSearchText, item.RequestId);
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

        private static string ResolveTodayScope(DashboardWorkItem item)
        {
            return item.Scope switch
            {
                DashboardScope.PendingRequests => DashboardScopeFilters.PendingRequests,
                DashboardScope.ExpiredFollowUp => DashboardScopeFilters.ExpiryFollowUps,
                DashboardScope.ExpiringSoon => DashboardScopeFilters.ExpiryFollowUps,
                _ => DashboardScopeFilters.AllWork
            };
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
