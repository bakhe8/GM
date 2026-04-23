using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Views
{
    public partial class CreateReductionRequestWindow : Window
    {
        private readonly List<Guarantee> _guarantees;

        public int SelectedGuaranteeId { get; private set; }
        public decimal RequestedAmount { get; private set; }
        public string RequestNotes { get; private set; } = string.Empty;
        public string CreatedBy { get; private set; } = string.Empty;

        public CreateReductionRequestWindow(List<Guarantee> guarantees, int? preselectedGuaranteeId = null)
        {
            InitializeComponent();
            _guarantees = guarantees;
            CmbGuarantee.ItemsSource = _guarantees;
            TxtCreatedBy.Text = Environment.UserName;
            TxtRequestedAmount.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtCreatedBy.TextChanged += (_, _) => UpdateSaveAvailability();

            if (_guarantees.Count > 0)
            {
                Guarantee? selectedGuarantee = preselectedGuaranteeId.HasValue
                    ? _guarantees.FirstOrDefault(g => g.Id == preselectedGuaranteeId.Value)
                    : null;

                CmbGuarantee.SelectedItem = selectedGuarantee ?? _guarantees[0];
            }

            UpdateSaveAvailability();
        }

        private void Guarantee_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                TxtSupplier.Text = string.Empty;
                TxtBank.Text = string.Empty;
                TxtCurrentAmount.Text = string.Empty;
                return;
            }

            TxtSupplier.Text = guarantee.Supplier;
            TxtBank.Text = guarantee.Bank;
            TxtCurrentAmount.Text = guarantee.Amount.ToString("N2");

            if (string.IsNullOrWhiteSpace(TxtRequestedAmount.Text))
            {
                TxtRequestedAmount.Text = Math.Max(guarantee.Amount - 1, 1).ToString("F2");
            }

            UpdateSaveAvailability();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                AppDialogService.ShowWarning("يرجى اختيار الضمان المطلوب قبل الحفظ.");
                return;
            }

            if (!decimal.TryParse(TxtRequestedAmount.Text.Trim(), out decimal requestedAmount))
            {
                AppDialogService.ShowWarning("يرجى إدخال مبلغ صالح للتخفيض.");
                return;
            }

            if (requestedAmount <= 0 || requestedAmount >= guarantee.Amount)
            {
                AppDialogService.ShowWarning("المبلغ المطلوب بعد التخفيض يجب أن يكون أكبر من صفر وأقل من المبلغ الحالي.");
                return;
            }

            SelectedGuaranteeId = guarantee.Id;
            RequestedAmount = requestedAmount;
            RequestNotes = TxtNotes.Text.Trim();
            CreatedBy = TxtCreatedBy.Text.Trim();

            if (string.IsNullOrWhiteSpace(CreatedBy))
            {
                AppDialogService.ShowWarning("يرجى إدخال اسم منشئ الطلب قبل الحفظ.");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateSaveAvailability()
        {
            bool canSave = IsSaveReady(out string reason);
            BtnSave.IsEnabled = canSave;
            BtnSave.ToolTip = canSave
                ? "أكد البيانات وأغلق هذه النافذة لإكمال إنشاء طلب التخفيض من الشاشة الأصلية."
                : $"غير متاح الآن - {reason}";
            ToolTipService.SetShowOnDisabled(BtnSave, true);
            TxtSaveGuidance.Text = canSave
                ? "النافذة جاهزة للمتابعة إلى إنشاء طلب التخفيض."
                : reason;
        }

        private bool IsSaveReady(out string reason)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                reason = "اختر الضمان المطلوب أولًا.";
                return false;
            }

            if (!decimal.TryParse(TxtRequestedAmount.Text.Trim(), out decimal requestedAmount))
            {
                reason = "أدخل مبلغًا صالحًا للتخفيض أولًا.";
                return false;
            }

            if (requestedAmount <= 0 || requestedAmount >= guarantee.Amount)
            {
                reason = "اجعل المبلغ المطلوب أكبر من صفر وأقل من المبلغ الحالي.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtCreatedBy.Text))
            {
                reason = "أدخل اسم منشئ الطلب أولًا.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
