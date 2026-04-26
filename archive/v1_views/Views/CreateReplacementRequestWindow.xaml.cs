using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Views
{
    public partial class CreateReplacementRequestWindow : Window
    {
        private readonly List<Guarantee> _guarantees;

        public int SelectedGuaranteeId { get; private set; }
        public string ReplacementGuaranteeNo { get; private set; } = string.Empty;
        public string ReplacementSupplier { get; private set; } = string.Empty;
        public string ReplacementBank { get; private set; } = string.Empty;
        public decimal ReplacementAmount { get; private set; }
        public DateTime ReplacementExpiryDate { get; private set; }
        public string ReplacementGuaranteeType { get; private set; } = string.Empty;
        public string ReplacementBeneficiary { get; private set; } = string.Empty;
        public GuaranteeReferenceType ReplacementReferenceType { get; private set; } = GuaranteeReferenceType.Contract;
        public string ReplacementReferenceNumber { get; private set; } = string.Empty;
        public string RequestNotes { get; private set; } = string.Empty;
        public string CreatedBy { get; private set; } = string.Empty;

        public CreateReplacementRequestWindow(List<Guarantee> guarantees, int? preselectedGuaranteeId = null)
        {
            InitializeComponent();
            _guarantees = guarantees;
            CmbGuarantee.ItemsSource = _guarantees;
            TxtCreatedBy.Text = Environment.UserName;
            TxtReplacementGuaranteeNo.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtReplacementSupplier.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtReplacementBank.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtReplacementAmount.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtReplacementGuaranteeType.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtReplacementReferenceNumber.TextChanged += (_, _) => UpdateSaveAvailability();
            TxtCreatedBy.TextChanged += (_, _) => UpdateSaveAvailability();
            DateReplacementExpiry.SelectedDateChanged += (_, _) => UpdateSaveAvailability();
            RadReplacementContract.Checked += (_, _) => UpdateSaveAvailability();
            RadReplacementPO.Checked += (_, _) => UpdateSaveAvailability();

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
                TxtCurrentSupplier.Text = string.Empty;
                TxtCurrentBank.Text = string.Empty;
                return;
            }

            TxtCurrentSupplier.Text = guarantee.Supplier;
            TxtCurrentBank.Text = guarantee.Bank;

            TxtReplacementSupplier.Text = guarantee.Supplier;
            TxtReplacementBank.Text = guarantee.Bank;
            TxtReplacementAmount.Text = guarantee.Amount.ToString("N2", CultureInfo.InvariantCulture);
            DateReplacementExpiry.SelectedDate = guarantee.ExpiryDate.Date;
            TxtReplacementGuaranteeType.Text = guarantee.GuaranteeType;
            TxtReplacementBeneficiary.Text = guarantee.Beneficiary;
            RadReplacementContract.IsChecked = guarantee.ReferenceType != GuaranteeReferenceType.PurchaseOrder;
            RadReplacementPO.IsChecked = guarantee.ReferenceType == GuaranteeReferenceType.PurchaseOrder;
            TxtReplacementReferenceNumber.Text = guarantee.ReferenceNumber;
            if (string.IsNullOrWhiteSpace(TxtReplacementGuaranteeNo.Text))
            {
                TxtReplacementGuaranteeNo.Focus();
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

            if (string.IsNullOrWhiteSpace(TxtReplacementGuaranteeNo.Text))
            {
                AppDialogService.ShowWarning("يرجى إدخال رقم الضمان البديل.");
                return;
            }

            if (!decimal.TryParse(TxtReplacementAmount.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) &&
                !decimal.TryParse(TxtReplacementAmount.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                AppDialogService.ShowWarning("يرجى إدخال مبلغ صالح للضمان البديل.");
                return;
            }

            if (!DateReplacementExpiry.SelectedDate.HasValue)
            {
                AppDialogService.ShowWarning("يرجى اختيار تاريخ انتهاء الضمان البديل.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtReplacementSupplier.Text) ||
                string.IsNullOrWhiteSpace(TxtReplacementBank.Text) ||
                string.IsNullOrWhiteSpace(TxtReplacementGuaranteeType.Text))
            {
                AppDialogService.ShowWarning("بيانات الضمان البديل الأساسية مطلوبة: المورد، البنك، ونوع الضمان.");
                return;
            }

            string referenceNumber = TxtReplacementReferenceNumber.Text.Trim();
            GuaranteeReferenceType referenceType = RadReplacementPO.IsChecked == true
                ? GuaranteeReferenceType.PurchaseOrder
                : GuaranteeReferenceType.Contract;

            if (string.IsNullOrWhiteSpace(referenceNumber))
            {
                AppDialogService.ShowWarning("يرجى إدخال رقم المرجع للضمان البديل.");
                return;
            }

            SelectedGuaranteeId = guarantee.Id;
            ReplacementGuaranteeNo = TxtReplacementGuaranteeNo.Text.Trim();
            ReplacementSupplier = TxtReplacementSupplier.Text.Trim();
            ReplacementBank = TxtReplacementBank.Text.Trim();
            ReplacementAmount = amount;
            ReplacementExpiryDate = DateReplacementExpiry.SelectedDate.Value.Date;
            ReplacementGuaranteeType = TxtReplacementGuaranteeType.Text.Trim();
            ReplacementBeneficiary = TxtReplacementBeneficiary.Text.Trim();
            ReplacementReferenceType = referenceType;
            ReplacementReferenceNumber = referenceNumber;
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
                ? "أكد البيانات وأغلق هذه النافذة لإكمال إنشاء طلب الاستبدال من الشاشة الأصلية."
                : $"غير متاح الآن - {reason}";
            ToolTipService.SetShowOnDisabled(BtnSave, true);
            TxtSaveGuidance.Text = canSave
                ? "النافذة جاهزة للمتابعة إلى إنشاء طلب الاستبدال."
                : reason;
        }

        private bool IsSaveReady(out string reason)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee)
            {
                reason = "اختر الضمان الحالي أولًا.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtReplacementGuaranteeNo.Text))
            {
                reason = "أدخل رقم الضمان البديل أولًا.";
                return false;
            }

            if (!decimal.TryParse(TxtReplacementAmount.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) &&
                !decimal.TryParse(TxtReplacementAmount.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                reason = "أدخل مبلغًا صالحًا للضمان البديل.";
                return false;
            }

            if (!DateReplacementExpiry.SelectedDate.HasValue)
            {
                reason = "اختر تاريخ انتهاء الضمان البديل أولًا.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtReplacementSupplier.Text) ||
                string.IsNullOrWhiteSpace(TxtReplacementBank.Text) ||
                string.IsNullOrWhiteSpace(TxtReplacementGuaranteeType.Text))
            {
                reason = "أكمل المورد والبنك ونوع الضمان البديل أولًا.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtReplacementReferenceNumber.Text))
            {
                reason = "أدخل رقم المرجع للضمان البديل أولًا.";
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
