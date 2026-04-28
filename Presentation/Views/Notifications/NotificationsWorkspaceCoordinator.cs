using System;
using System.Windows;
using GuaranteeManager.Models;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class NotificationsWorkspaceCoordinator
    {
        public void OpenGuaranteeContext(
            NotificationWorkspaceItem? item,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
            Action showGuarantees)
        {
            if (item == null)
            {
                return;
            }

            int rootId = item.Guarantee.RootId ?? item.Guarantee.Id;
            if (rootId > 0)
            {
                openGuaranteeContext(rootId, GuaranteeFileFocusArea.Actions, null);
                return;
            }

            showGuarantees();
        }

        public void CopyGuarantee(NotificationWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.GuaranteeNo, "رقم الضمان");
        }

        public void CopyBank(NotificationWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Bank, "البنك");
        }

        public void CopyAmount(NotificationWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.AmountDisplay, "القيمة");
        }

        private static void CopyText(string value, string label)
        {
            try
            {
                Clipboard.SetText(value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
