using System;
using System.Windows;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class NotificationsWorkspaceCoordinator
    {
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
