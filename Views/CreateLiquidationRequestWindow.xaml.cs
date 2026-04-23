using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Views
{
    public partial class CreateLiquidationRequestWindow : Window
    {
        private readonly List<Guarantee> _guarantees;

        public int SelectedGuaranteeId { get; private set; }
        public string RequestNotes { get; private set; } = string.Empty;
        public string CreatedBy { get; private set; } = string.Empty;

        public CreateLiquidationRequestWindow(List<Guarantee> guarantees, int? preselectedGuaranteeId = null)
        {
            InitializeComponent();
            _guarantees = guarantees;
            CmbGuarantee.ItemsSource = _guarantees;
            TxtCreatedBy.Text = Environment.UserName;
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
                return;
            }

            TxtSupplier.Text = guarantee.Supplier;
            TxtBank.Text = guarantee.Bank;
            UpdateSaveAvailability();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee guarantee)
            {
                AppDialogService.ShowWarning("يرجى اختيار الضمان المطلوب قبل الحفظ.");
                return;
            }

            SelectedGuaranteeId = guarantee.Id;
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
                ? "أكد البيانات وأغلق هذه النافذة لإكمال إنشاء طلب التسييل من الشاشة الأصلية."
                : $"غير متاح الآن - {reason}";
            ToolTipService.SetShowOnDisabled(BtnSave, true);
            TxtSaveGuidance.Text = canSave
                ? "النافذة جاهزة للمتابعة إلى إنشاء طلب التسييل."
                : reason;
        }

        private bool IsSaveReady(out string reason)
        {
            if (CmbGuarantee.SelectedItem is not Guarantee)
            {
                reason = "اختر الضمان المطلوب أولًا.";
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
