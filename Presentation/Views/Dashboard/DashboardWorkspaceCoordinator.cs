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

        public void CopyGuaranteeNo(DashboardWorkItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Reference, "رقم الضمان", BuildStatusContext(item));
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
    }
}
