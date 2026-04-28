using System;
using System.Windows;
using GuaranteeManager.Services;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceCoordinator
    {
        private readonly IShellStatusService _shellStatus;

        public BanksWorkspaceCoordinator()
        {
            _shellStatus = App.CurrentApp.GetRequiredService<IShellStatusService>();
        }

        public void CopyBank(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.Bank, "اسم البنك", $"البنوك • {item.Bank}");
        }

        public void CopyAmount(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.AmountDisplay, "إجمالي قيمة البنك", $"البنوك • {item.Bank}");
        }

        public void CopyBeneficiary(BankWorkspaceItem? item)
        {
            if (item == null)
            {
                return;
            }

            CopyText(item.TopBeneficiary, "اسم المستفيد الأعلى", $"البنوك • {item.Bank}");
        }

        private void CopyText(string value, string label, string secondaryText)
        {
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
    }
}
