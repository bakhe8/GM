using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed record ReplacementRequestInput(
        string ReplacementGuaranteeNo,
        string ReplacementSupplier,
        string ReplacementBank,
        decimal ReplacementAmount,
        DateTime ReplacementExpiryDate,
        string ReplacementGuaranteeType,
        string ReplacementBeneficiary,
        GuaranteeReferenceType ReplacementReferenceType,
        string ReplacementReferenceNumber,
        string Notes);

    public sealed class ReplacementRequestDialog : Window
    {
        private readonly Func<string, bool> _isGuaranteeNoUnique;
        private readonly Guarantee _currentGuarantee;
        private readonly TextBox _guaranteeNoInput = BuildTextBox();
        private readonly TextBox _supplierInput = BuildTextBox();
        private readonly ComboBox _bankInput = BuildEditableComboBox();
        private readonly TextBox _amountInput = BuildTextBox();
        private readonly TextBox _expiryInput = BuildTextBox();
        private readonly ComboBox _typeInput = BuildEditableComboBox();
        private readonly TextBox _beneficiaryInput = BuildTextBox();
        private readonly ComboBox _referenceTypeInput = new();
        private readonly TextBox _referenceNumberInput = BuildTextBox();
        private readonly TextBox _notesInput = BuildTextBox();

        private ReplacementRequestInput? _input;

        private ReplacementRequestDialog(
            Guarantee currentGuarantee,
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique)
        {
            _currentGuarantee = currentGuarantee;
            _isGuaranteeNoUnique = isGuaranteeNoUnique;

            Title = "طلب استبدال";
            Width = 520;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(ReplacementRequestDialog), TryAccept, "احفظ طلب الاستبدال أو أغلق نافذته أولاً.");

            foreach (string bank in banks.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _bankInput.Items.Add(bank);
            }

            foreach (string guaranteeType in guaranteeTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _typeInput.Items.Add(guaranteeType);
            }

            _supplierInput.Text = string.IsNullOrWhiteSpace(currentGuarantee.Supplier) ? string.Empty : currentGuarantee.Supplier;
            _bankInput.Text = currentGuarantee.Bank;
            _amountInput.Text = currentGuarantee.Amount.ToString("N2", CultureInfo.InvariantCulture);
            _expiryInput.Text = currentGuarantee.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            _typeInput.Text = currentGuarantee.GuaranteeType;
            _beneficiaryInput.Text = BusinessPartyDefaults.NormalizeBeneficiary(currentGuarantee.Beneficiary);
            _beneficiaryInput.IsReadOnly = true;
            _referenceNumberInput.Text = currentGuarantee.ReferenceNumber;
            _notesInput.Text = $"استبدال للضمان {currentGuarantee.GuaranteeNo}";
            _notesInput.Height = 58;
            _notesInput.AcceptsReturn = true;
            _notesInput.TextWrapping = TextWrapping.Wrap;

            _referenceTypeInput.ItemsSource = new List<ReferenceTypeOption>
            {
                new(GuaranteeReferenceType.Contract, "عقد"),
                new(GuaranteeReferenceType.PurchaseOrder, "أمر شراء"),
                new(GuaranteeReferenceType.None, "بدون مرجع")
            };
            _referenceTypeInput.DisplayMemberPath = nameof(ReferenceTypeOption.Label);
            ReferenceTypeOption selectedReferenceType = ((IEnumerable<ReferenceTypeOption>)_referenceTypeInput.ItemsSource)
                .FirstOrDefault(option => option.Value == currentGuarantee.ReferenceType)
                ?? ((IEnumerable<ReferenceTypeOption>)_referenceTypeInput.ItemsSource).First();
            _referenceTypeInput.SelectedItem = selectedReferenceType;
            _referenceTypeInput.Height = 34;
            _referenceTypeInput.Margin = new Thickness(0, 4, 0, 10);

            var root = new DockPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fields = new StackPanel();

            fields.Children.Add(BuildField("رقم الضمان الحالي", BuildReadOnlyValue($"{currentGuarantee.GuaranteeNo} | {currentGuarantee.Supplier}")));
            fields.Children.Add(BuildField("رقم الضمان البديل", _guaranteeNoInput));
            fields.Children.Add(BuildField("المورد البديل", _supplierInput));
            fields.Children.Add(BuildField("البنك البديل", _bankInput));
            fields.Children.Add(BuildField("المبلغ البديل", _amountInput));
            fields.Children.Add(BuildField("تاريخ الانتهاء البديل", _expiryInput));
            fields.Children.Add(BuildField("نوع الضمان البديل", _typeInput));
            fields.Children.Add(BuildField("الجهة المستفيدة للبديل", _beneficiaryInput));
            fields.Children.Add(BuildField("نوع المرجع", _referenceTypeInput));
            fields.Children.Add(BuildField("رقم المرجع", _referenceNumberInput));
            fields.Children.Add(BuildField("ملاحظات", _notesInput));

            scroll.Content = fields;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var saveButton = new Button { Content = "حفظ", Width = 90, Height = 32, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
            saveButton.Click += (_, _) => TryAccept();
            var cancelButton = new Button { Content = "إلغاء", Width = 90, Height = 32, IsCancel = true };
            actions.Children.Add(saveButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);
            root.Children.Add(scroll);

            Content = root;
        }

        public static bool TryShow(
            Guarantee currentGuarantee,
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique,
            out ReplacementRequestInput input)
        {
            var dialog = new ReplacementRequestDialog(currentGuarantee, banks, guaranteeTypes, isGuaranteeNoUnique)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true && dialog._input != null;
            input = dialog._input ?? new ReplacementRequestInput(
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                DateTime.Today,
                string.Empty,
                string.Empty,
                GuaranteeReferenceType.None,
                string.Empty,
                string.Empty);
            return accepted;
        }

        private void TryAccept()
        {
            string replacementGuaranteeNo = _guaranteeNoInput.Text.Trim();
            string replacementSupplier = _supplierInput.Text.Trim();
            string replacementBank = GetComboText(_bankInput);
            string amountText = _amountInput.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string replacementGuaranteeType = GetComboText(_typeInput);
            string replacementBeneficiary = _beneficiaryInput.Text.Trim();
            string replacementReferenceNumber = _referenceNumberInput.Text.Trim();
            string notes = _notesInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(replacementGuaranteeNo))
            {
                MessageBox.Show("أدخل رقم الضمان البديل أولاً.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isGuaranteeNoUnique(replacementGuaranteeNo))
            {
                MessageBox.Show("رقم الضمان البديل مستخدم مسبقًا.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(replacementSupplier)
                || string.IsNullOrWhiteSpace(replacementBank)
                || string.IsNullOrWhiteSpace(replacementGuaranteeType))
            {
                MessageBox.Show("أكمل المورد والبنك ونوع الضمان البديل.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal replacementAmount) || replacementAmount <= 0)
            {
                MessageBox.Show("أدخل مبلغًا صحيحًا أكبر من صفر.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime replacementExpiryDate))
            {
                MessageBox.Show("صيغة تاريخ انتهاء الضمان البديل غير صحيحة. استخدم مثلاً 2026/12/31.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(replacementReferenceNumber))
            {
                MessageBox.Show("أدخل رقم المرجع للضمان البديل.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GuaranteeReferenceType replacementReferenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;
            _input = new ReplacementRequestInput(
                replacementGuaranteeNo,
                replacementSupplier,
                replacementBank,
                replacementAmount,
                replacementExpiryDate.Date,
                replacementGuaranteeType,
                BusinessPartyDefaults.NormalizeBeneficiary(replacementBeneficiary),
                replacementReferenceType,
                replacementReferenceNumber,
                string.IsNullOrWhiteSpace(notes) ? $"استبدال للضمان {_currentGuarantee.GuaranteeNo}" : notes);
            DialogResult = true;
        }

        private static FrameworkElement BuildReadOnlyValue(string text)
        {
            return new Border
            {
                Height = 34,
                Padding = new Thickness(8, 0, 8, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static string GetComboText(ComboBox comboBox)
            => (comboBox.Text ?? comboBox.SelectedItem?.ToString() ?? string.Empty).Trim();

        private static FrameworkElement BuildField(string label, FrameworkElement input)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(BuildLabel(label));
            input.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(input);
            return panel;
        }

        private static TextBlock BuildLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            };
        }

        private static TextBox BuildTextBox()
        {
            return new TextBox
            {
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
        }

        private static ComboBox BuildEditableComboBox()
        {
            return new ComboBox
            {
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                IsEditable = true
            };
        }

        private sealed record ReferenceTypeOption(GuaranteeReferenceType Value, string Label);
    }
}
