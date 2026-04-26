using System;
using System.Windows;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceCoordinator
    {
        public void CopyBank(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Bank, "اسم البنك");
        }

        public void CopyAmount(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.AmountDisplay, "قيمة البنك");
        }

        public void CopyBeneficiary(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.TopBeneficiary, "المستفيد");
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
