using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Views
{
    public partial class CreateExtensionRequestWindow : Window
    {
        private readonly List<Guarantee> _guarantees;

        public int SelectedGuaranteeId { get; private set; }
        public DateTime RequestedExpiryDate { get; private set; }
        public string RequestNotes { get; private set; } = string.Empty;
        public string CreatedBy { get; private set; } = string.Empty;

        public CreateExtensionRequestWindow(List<Guarantee> guarantees, int? preselectedGuaranteeId = null)
        {
            InitializeComponent();
            _guarantees = guarantees;
            CmbGuarantee.ItemsSource = _guarantees;
            TxtCreatedBy.Text = Environment.UserName;
            DateRequestedExpiry.SelectedDateChanged += (_, _) => UpdateSaveAvailability();
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
                TxtCurrentExpiry.Text = string.Empty;
                return;
            }

            TxtSupplier.Text = guarantee.Supplier;
            TxtBank.Text = guarantee.Bank;
            TxtCurrentExpiry.Text = guarantee.ExpiryDate.ToString("yyyy-MM-dd");

            if (!DateRequestedExpiry.SelectedDate.HasValue || DateRequestedExpiry.SelectedDate.Value.Date <= guarantee.ExpiryDate.Date)
            {
                DateRequestedExpiry.SelectedDate = guarantee.ExpiryDate.AddMonths(1);
            }

            UpdateSaveAvailability();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                AppDialogService.ShowWarning("يرجى اختيار الضمان المطلوب قبل الحفظ.");
                return;
            }

            if (!DateRequestedExpiry.SelectedDate.HasValue)
            {
                AppDialogService.ShowWarning("يرجى تحديد التاريخ المطلوب بعد التمديد.");
                return;
            }

            if (DateRequestedExpiry.SelectedDate.Value.Date <= guarantee.ExpiryDate.Date)
            {
                AppDialogService.ShowWarning("تاريخ التمديد المطلوب يجب أن يكون بعد تاريخ الانتهاء الحالي.");
                return;
            }

            SelectedGuaranteeId = guarantee.Id;
            RequestedExpiryDate = DateRequestedExpiry.SelectedDate.Value.Date;
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
                ? "أكد البيانات وأغلق هذه النافذة لإكمال إنشاء طلب التمديد من الشاشة الأصلية."
                : $"غير متاح الآن - {reason}";
            ToolTipService.SetShowOnDisabled(BtnSave, true);
            TxtSaveGuidance.Text = canSave
                ? "النافذة جاهزة للمتابعة إلى إنشاء طلب التمديد."
                : reason;
        }

        private bool IsSaveReady(out string reason)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                reason = "اختر الضمان المطلوب أولًا.";
                return false;
            }

            if (!DateRequestedExpiry.SelectedDate.HasValue)
            {
                reason = "اختر التاريخ المطلوب بعد التمديد أولًا.";
                return false;
            }

            if (DateRequestedExpiry.SelectedDate.Value.Date <= guarantee.ExpiryDate.Date)
            {
                reason = "اجعل تاريخ التمديد المطلوب بعد تاريخ الانتهاء الحالي.";
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
