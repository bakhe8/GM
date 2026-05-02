using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Microsoft.Win32;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed record NewGuaranteeInput(
        string GuaranteeNo,
        string Supplier,
        string Beneficiary,
        string Bank,
        string GuaranteeType,
        decimal Amount,
        DateTime ExpiryDate,
        GuaranteeReferenceType ReferenceType,
        string ReferenceNumber,
        string Notes,
        List<AttachmentInput> Attachments);

    public sealed class NewGuaranteeDialog : Window
    {
        private readonly Func<string, bool> _isGuaranteeNoUnique;
        private readonly TextBox _guaranteeNoInput = BuildTextBox();
        private readonly TextBox _supplierInput = BuildTextBox();
        private readonly TextBox _beneficiaryInput = BuildTextBox();
        private readonly ComboBox _bankInput = BuildEditableComboBox();
        private readonly ComboBox _typeInput = BuildEditableComboBox();
        private readonly TextBox _amountInput = BuildTextBox();
        private readonly TextBox _expiryInput = BuildTextBox();
        private readonly ComboBox _referenceTypeInput = new();
        private readonly TextBox _referenceNumberInput = BuildTextBox();
        private readonly TextBox _notesInput = BuildTextBox();
        private readonly ComboBox _attachmentTypeInput = new();
        private readonly TextBlock _attachmentsLabel = new();
        private readonly StackPanel _attachmentsList = new() { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<AttachmentInput> _attachments = new();
        private readonly Border _consequenceCard = new();
        private readonly TextBlock _consequenceSummary = new();
        private readonly TextBlock _consequencePrimary = new();
        private readonly TextBlock _consequenceSecondary = new();

        private NewGuaranteeInput? _input;
        private bool _isDirty;
        private bool _allowCloseWithoutPrompt;

        private NewGuaranteeDialog(
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique)
        {
            _isGuaranteeNoUnique = isGuaranteeNoUnique;
            Title = "إجراء جديد";
            UiInstrumentation.Identify(this, "Dialog.NewGuarantee", Title);
            UiInstrumentation.Identify(_guaranteeNoInput, "Dialog.NewGuarantee.GuaranteeNoInput", "رقم الضمان");
            UiInstrumentation.Identify(_supplierInput, "Dialog.NewGuarantee.SupplierInput", "المورد");
            UiInstrumentation.Identify(_beneficiaryInput, "Dialog.NewGuarantee.BeneficiaryInput", "الجهة المستفيدة");
            UiInstrumentation.Identify(_bankInput, "Dialog.NewGuarantee.BankInput", "البنك");
            UiInstrumentation.Identify(_typeInput, "Dialog.NewGuarantee.TypeInput", "نوع الضمان");
            UiInstrumentation.Identify(_amountInput, "Dialog.NewGuarantee.AmountInput", "المبلغ");
            UiInstrumentation.Identify(_expiryInput, "Dialog.NewGuarantee.ExpiryInput", "تاريخ الانتهاء");
            UiInstrumentation.Identify(_referenceTypeInput, "Dialog.NewGuarantee.ReferenceTypeInput", "نوع المرجع");
            UiInstrumentation.Identify(_referenceNumberInput, "Dialog.NewGuarantee.ReferenceNumberInput", "رقم المرجع");
            UiInstrumentation.Identify(_notesInput, "Dialog.NewGuarantee.NotesInput", "ملاحظات");
            UiInstrumentation.Identify(_attachmentTypeInput, "Dialog.NewGuarantee.AttachmentTypeInput", "نوع المستند");
            UiInstrumentation.Identify(_attachmentsLabel, "Dialog.NewGuarantee.AttachmentsSummary", "ملخص المرفقات");
            UiInstrumentation.Identify(_consequenceSummary, "Dialog.NewGuarantee.ConsequenceSummary", "معاينة أثر الحفظ");
            UiInstrumentation.Identify(_consequencePrimary, "Dialog.NewGuarantee.ConsequencePrimary", "تفاصيل أثر الحفظ");
            UiInstrumentation.Identify(_consequenceSecondary, "Dialog.NewGuarantee.ConsequenceSecondary", "ملاحظات أثر الحفظ");
            Width = 520;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(NewGuaranteeDialog), TryAccept, "احفظ الضمان الجديد أو أغلق النافذة الحالية أولاً.");

            foreach (string bank in banks.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _bankInput.Items.Add(bank);
            }

            foreach (string guaranteeType in guaranteeTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _typeInput.Items.Add(guaranteeType);
            }

            _amountInput.Text = "0";
            _expiryInput.Text = DateTime.Today.AddMonths(6).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            _beneficiaryInput.Text = BusinessPartyDefaults.DefaultBeneficiaryName;
            _beneficiaryInput.IsReadOnly = true;
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
            _referenceTypeInput.SelectedIndex = 0;
            _referenceTypeInput.Height = 34;
            _referenceTypeInput.Margin = new Thickness(0, 4, 0, 10);

            _attachmentTypeInput.ItemsSource = BuildAttachmentTypeOptions();
            _attachmentTypeInput.DisplayMemberPath = nameof(AttachmentDocumentTypeOption.Label);
            _attachmentTypeInput.SelectedIndex = 0;
            _attachmentTypeInput.Height = 30;
            _attachmentTypeInput.Width = 132;
            _attachmentTypeInput.Margin = new Thickness(0, 0, 8, 0);

            var root = new DockPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fields = new StackPanel();

            fields.Children.Add(BuildField("رقم الضمان", _guaranteeNoInput));
            fields.Children.Add(BuildField("المورد", _supplierInput));
            fields.Children.Add(BuildField("الجهة المستفيدة", _beneficiaryInput));
            fields.Children.Add(BuildField("البنك", _bankInput));
            fields.Children.Add(BuildField("نوع الضمان", _typeInput));
            fields.Children.Add(BuildField("المبلغ", _amountInput));
            fields.Children.Add(BuildField("تاريخ الانتهاء", _expiryInput));
            fields.Children.Add(BuildField("نوع المرجع", _referenceTypeInput));
            fields.Children.Add(BuildField("رقم المرجع", _referenceNumberInput));
            fields.Children.Add(BuildField("ملاحظات", _notesInput));
            fields.Children.Add(BuildConsequenceSection());

            var attachmentPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            attachmentPanel.Children.Add(BuildLabel("المرفقات"));
            var attachmentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var attachmentButton = UiInstrumentation.Identify(
                new Button { Content = "إضافة مرفقات", Width = 112, Height = 30 },
                "Dialog.NewGuarantee.AttachmentsButton",
                "إضافة مرفقات");
            attachmentButton.Click += (_, _) => PickAttachments();
            _attachmentsLabel.Text = "بدون مرفقات";
            _attachmentsLabel.FontSize = 11;
            _attachmentsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _attachmentsLabel.VerticalAlignment = VerticalAlignment.Center;
            _attachmentsLabel.Margin = new Thickness(10, 0, 0, 0);
            attachmentRow.Children.Add(_attachmentTypeInput);
            attachmentRow.Children.Add(attachmentButton);
            attachmentRow.Children.Add(_attachmentsLabel);
            attachmentPanel.Children.Add(attachmentRow);
            attachmentPanel.Children.Add(_attachmentsList);
            fields.Children.Add(attachmentPanel);

            scroll.Content = fields;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var saveButton = UiInstrumentation.Identify(
                new Button { Content = "حفظ", Width = 90, Height = 32, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) },
                "Dialog.NewGuarantee.SaveButton",
                "حفظ");
            saveButton.Click += (_, _) => TryAccept();
            var reportsButton = UiInstrumentation.Identify(
                new Button { Content = "التقارير", Width = 96, Height = 32, Margin = new Thickness(8, 0, 0, 0) },
                "Dialog.NewGuarantee.ReportsButton",
                "التقارير");
            reportsButton.Click += (_, _) => DialogFormSupport.RunWorkspaceReport(Title);
            var cancelButton = UiInstrumentation.Identify(
                new Button { Content = "إلغاء", Width = 90, Height = 32, IsCancel = true },
                "Dialog.NewGuarantee.CancelButton",
                "إلغاء");
            actions.Children.Add(saveButton);
            actions.Children.Add(reportsButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);
            root.Children.Add(scroll);

            Content = root;
            WireDirtyTracking();
            RefreshConsequencePreview();
            Closing += OnClosing;
        }

        public static bool TryShow(
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique,
            out NewGuaranteeInput input)
        {
            var dialog = new NewGuaranteeDialog(banks, guaranteeTypes, isGuaranteeNoUnique)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true && dialog._input != null;
            input = dialog._input ?? new NewGuaranteeInput(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                DateTime.Today,
                GuaranteeReferenceType.None,
                string.Empty,
                string.Empty,
                new List<AttachmentInput>());
            return accepted;
        }

        private void TryAccept()
        {
            string guaranteeNo = _guaranteeNoInput.Text.Trim();
            string supplier = _supplierInput.Text.Trim();
            string beneficiary = _beneficiaryInput.Text.Trim();
            string bank = GetComboText(_bankInput);
            string guaranteeType = GetComboText(_typeInput);
            string amountText = _amountInput.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string referenceNumber = _referenceNumberInput.Text.Trim();
            string notes = _notesInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(guaranteeNo)
                || string.IsNullOrWhiteSpace(supplier)
                || string.IsNullOrWhiteSpace(bank)
                || string.IsNullOrWhiteSpace(guaranteeType))
            {
                MessageBox.Show("أكمل رقم الضمان والمورد والبنك ونوع الضمان.", "إجراء جديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isGuaranteeNoUnique(guaranteeNo))
            {
                MessageBox.Show("رقم الضمان موجود مسبقاً.", "إجراء جديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("أدخل مبلغاً صحيحاً أكبر من صفر.", "إجراء جديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate))
            {
                MessageBox.Show("صيغة تاريخ الانتهاء غير صحيحة. استخدم مثلاً 2026/12/31.", "إجراء جديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GuaranteeReferenceType referenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;
            _input = new NewGuaranteeInput(
                guaranteeNo,
                supplier,
                BusinessPartyDefaults.NormalizeBeneficiary(beneficiary),
                bank,
                guaranteeType,
                amount,
                expiryDate,
                referenceType,
                referenceNumber,
                notes,
                [.. _attachments]);
            _allowCloseWithoutPrompt = true;
            DialogResult = true;
        }

        private void PickAttachments()
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختيار مرفقات الضمان",
                Filter = "ملفات المستندات|*.pdf;*.doc;*.docx;*.png;*.jpg;*.jpeg|كل الملفات|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            AttachmentDocumentType selectedType = (_attachmentTypeInput.SelectedItem as AttachmentDocumentTypeOption)?.Value
                ?? AttachmentDocumentType.SupportingDocument;

            foreach (string file in dialog.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_attachments.Any(existing => string.Equals(existing.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                {
                    _attachments.Add(new AttachmentInput(file, selectedType));
                }
            }

            RefreshAttachmentsState();
            MarkDirty();
        }

        private void RefreshAttachmentsState()
        {
            _attachmentsList.Children.Clear();
            _attachmentsLabel.Text = _attachments.Count == 0
                ? "بدون مرفقات"
                : $"{_attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق";

            foreach (AttachmentInput attachment in _attachments)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6), FlowDirection = FlowDirection.LeftToRight };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock
                {
                    Text = Path.GetFileName(attachment.FilePath),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FlowDirection = FlowDirection.LeftToRight
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var type = new TextBlock
                {
                    Text = attachment.DocumentTypeLabel,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(type, 1);
                row.Children.Add(type);

                var removeButton = new Button
                {
                    Content = "إزالة",
                    Width = 68,
                    Height = 28,
                    FontSize = 10,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7C5C5")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                };
                removeButton.Click += (_, _) =>
                {
                    _attachments.Remove(attachment);
                    RefreshAttachmentsState();
                    MarkDirty();
                };
                Grid.SetColumn(removeButton, 3);
                row.Children.Add(removeButton);

                _attachmentsList.Children.Add(row);
            }
        }

        private void WireDirtyTracking()
        {
            DialogFormSupport.WireDirtyTracking(
                MarkDirty,
                _guaranteeNoInput,
                _supplierInput,
                _beneficiaryInput,
                _bankInput,
                _typeInput,
                _amountInput,
                _expiryInput,
                _referenceTypeInput,
                _referenceNumberInput,
                _attachmentTypeInput,
                _notesInput);
            _isDirty = false;
        }

        private void MarkDirty()
        {
            _isDirty = true;
            RefreshConsequencePreview();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_allowCloseWithoutPrompt)
            {
                return;
            }

            if (!_isDirty || !HasMeaningfulChanges())
            {
                return;
            }

            if (DialogFormSupport.ConfirmDiscardChanges())
            {
                _allowCloseWithoutPrompt = true;
                return;
            }

            e.Cancel = true;
        }

        private bool HasMeaningfulChanges()
        {
            string guaranteeNo = _guaranteeNoInput.Text.Trim();
            string supplier = _supplierInput.Text.Trim();
            string beneficiary = _beneficiaryInput.Text.Trim();
            string bank = GetComboText(_bankInput);
            string guaranteeType = GetComboText(_typeInput);
            string amountText = _amountInput.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string referenceNumber = _referenceNumberInput.Text.Trim();
            string notes = _notesInput.Text.Trim();
            GuaranteeReferenceType referenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;

            bool amountChanged = !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount != 0;
            bool expiryChanged = !DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate)
                                 || expiryDate.Date != DateTime.Today.AddMonths(6).Date;

            return !string.IsNullOrWhiteSpace(guaranteeNo)
                   || !string.IsNullOrWhiteSpace(supplier)
                   || !string.IsNullOrWhiteSpace(beneficiary)
                   || !string.IsNullOrWhiteSpace(bank)
                   || !string.IsNullOrWhiteSpace(guaranteeType)
                   || amountChanged
                   || expiryChanged
                   || referenceType != GuaranteeReferenceType.Contract
                   || !string.IsNullOrWhiteSpace(referenceNumber)
                   || !string.IsNullOrWhiteSpace(notes)
                   || _attachments.Count > 0;
        }

        private static string GetComboText(ComboBox comboBox)
            => (comboBox.Text ?? comboBox.SelectedItem?.ToString() ?? string.Empty).Trim();

        private static FrameworkElement BuildField(string label, Control input)
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

        private FrameworkElement BuildConsequenceSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            panel.Children.Add(BuildLabel("أثر الحفظ"));

            ConfigureConsequenceText(_consequenceSummary, 12, FontWeights.SemiBold, "#0F172A");
            ConfigureConsequenceText(_consequencePrimary, 11, FontWeights.Normal, "#64748B");
            ConfigureConsequenceText(_consequenceSecondary, 11, FontWeights.Normal, "#64748B");
            _consequencePrimary.Margin = new Thickness(0, 4, 0, 0);
            _consequenceSecondary.Margin = new Thickness(0, 4, 0, 0);

            _consequenceCard.CornerRadius = new CornerRadius(8);
            _consequenceCard.BorderThickness = new Thickness(1);
            _consequenceCard.Padding = new Thickness(12, 10, 12, 10);
            _consequenceCard.Margin = new Thickness(0, 4, 0, 0);
            _consequenceCard.Child = new StackPanel
            {
                Children =
                {
                    _consequenceSummary,
                    _consequencePrimary,
                    _consequenceSecondary
                }
            };

            panel.Children.Add(_consequenceCard);
            return panel;
        }

        private void RefreshConsequencePreview()
        {
            string guaranteeNo = _guaranteeNoInput.Text.Trim();
            string supplier = _supplierInput.Text.Trim();
            string beneficiary = _beneficiaryInput.Text.Trim();
            string bank = GetComboText(_bankInput);
            string guaranteeType = GetComboText(_typeInput);
            string amountText = _amountInput.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string referenceNumber = _referenceNumberInput.Text.Trim();
            GuaranteeReferenceType referenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(guaranteeNo))
            {
                missing.Add("رقم الضمان");
            }

            if (string.IsNullOrWhiteSpace(supplier))
            {
                missing.Add("المورد");
            }

            if (string.IsNullOrWhiteSpace(bank))
            {
                missing.Add("البنك");
            }

            if (string.IsNullOrWhiteSpace(guaranteeType))
            {
                missing.Add("نوع الضمان");
            }

            bool amountValid = decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) && amount > 0;
            bool expiryValid = DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate);

            if (missing.Count > 0 || !amountValid || !expiryValid)
            {
                var blockers = new List<string>();
                if (missing.Count > 0)
                {
                    blockers.Add($"أكمل: {string.Join("، ", missing)}");
                }

                if (!amountValid)
                {
                    blockers.Add("المبلغ يجب أن يكون أكبر من صفر.");
                }

                if (!expiryValid)
                {
                    blockers.Add("تاريخ الانتهاء غير صالح بعد.");
                }

                SetConsequenceState(
                    "الحفظ لن يكتمل بعد.",
                    blockers.FirstOrDefault() ?? "أكمل البيانات الأساسية أولًا.",
                    blockers.Count > 1 ? string.Join(" ", blockers.Skip(1)) : "عند اكتمال البيانات سيُنشأ الضمان مباشرة كإصدار أول.",
                    warningState: true);
                return;
            }

            string effectiveBeneficiary = BusinessPartyDefaults.NormalizeBeneficiary(beneficiary);
            string referenceSummary = BuildReferenceSummary(referenceType, referenceNumber);
            string attachmentSummary = _attachments.Count == 0
                ? "بدون مرفقات إضافية"
                : $"{_attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق سيُحفظ";
            string beneficiarySummary = string.IsNullOrWhiteSpace(beneficiary)
                ? $"سيُستخدم المستفيد الافتراضي: {effectiveBeneficiary}"
                : $"الجهة المستفيدة: {effectiveBeneficiary}";

            SetConsequenceState(
                "سيُنشأ ضمان جديد كالإصدار الأول.",
                $"{guaranteeNo} • {bank} • {guaranteeType} • {amount.ToString("N2", CultureInfo.InvariantCulture)} ريال • {expiryDate:yyyy/MM/dd}",
                $"{beneficiarySummary} • {referenceSummary} • {attachmentSummary}",
                warningState: false);
        }

        private void SetConsequenceState(string summary, string primary, string secondary, bool warningState)
        {
            ApplyConsequenceText(_consequenceSummary, summary);
            ApplyConsequenceText(_consequencePrimary, primary);
            ApplyConsequenceText(_consequenceSecondary, secondary);

            _consequenceCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#FFF9EC" : "#EFF6FF"));
            _consequenceCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#F6DE99" : "#BFDBFE"));
            _consequenceSummary.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#E09408" : "#1D4ED8"));
        }

        private static void ConfigureConsequenceText(TextBlock textBlock, double fontSize, FontWeight fontWeight, string colorHex)
        {
            textBlock.FontSize = fontSize;
            textBlock.FontWeight = fontWeight;
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private static void ApplyConsequenceText(TextBlock textBlock, string value)
        {
            string resolved = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            textBlock.Text = resolved;
            System.Windows.Automation.AutomationProperties.SetHelpText(textBlock, resolved);
            System.Windows.Automation.AutomationProperties.SetItemStatus(textBlock, resolved);
        }

        private static string BuildReferenceSummary(GuaranteeReferenceType referenceType, string referenceNumber)
        {
            string number = referenceNumber?.Trim() ?? string.Empty;
            return referenceType switch
            {
                GuaranteeReferenceType.Contract when !string.IsNullOrWhiteSpace(number) => $"عقد: {number}",
                GuaranteeReferenceType.PurchaseOrder when !string.IsNullOrWhiteSpace(number) => $"أمر شراء: {number}",
                _ => "بدون مرجع"
            };
        }

        private static List<AttachmentDocumentTypeOption> BuildAttachmentTypeOptions()
        {
            return AttachmentDocumentTypeText.OfficialAttachmentTypes
                .Select(type => new AttachmentDocumentTypeOption(type, AttachmentDocumentTypeText.Label(type)))
                .ToList();
        }

        private sealed record ReferenceTypeOption(GuaranteeReferenceType Value, string Label);
    }
}
