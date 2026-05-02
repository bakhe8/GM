using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public sealed record EditGuaranteeInput(
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
        List<AttachmentInput> NewAttachments,
        List<AttachmentRecord> RemovedAttachments);

    public sealed class EditGuaranteeDialog : Window
    {
        private readonly Guarantee _currentGuarantee;
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
        private readonly TextBlock _existingAttachmentsLabel = new();
        private readonly StackPanel _existingAttachmentsList = new() { Margin = new Thickness(0, 8, 0, 0) };
        private readonly ComboBox _newAttachmentTypeInput = new();
        private readonly TextBlock _newAttachmentsLabel = new();
        private readonly StackPanel _newAttachmentsList = new() { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<AttachmentInput> _newAttachments = new();
        private readonly HashSet<int> _removedAttachmentIds = new();
        private readonly Border _consequenceCard = new();
        private readonly TextBlock _consequenceSummary = new();
        private readonly TextBlock _consequencePrimary = new();
        private readonly TextBlock _consequenceSecondary = new();

        private EditGuaranteeInput? _input;
        private bool _isDirty;
        private bool _allowCloseWithoutPrompt;

        private EditGuaranteeDialog(
            Guarantee currentGuarantee,
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique)
        {
            _currentGuarantee = currentGuarantee;
            _isGuaranteeNoUnique = isGuaranteeNoUnique;

            Title = "تعديل الضمان";
            UiInstrumentation.Identify(this, "Dialog.EditGuarantee", Title);
            UiInstrumentation.Identify(_guaranteeNoInput, "Dialog.EditGuarantee.GuaranteeNoInput", "رقم الضمان");
            UiInstrumentation.Identify(_supplierInput, "Dialog.EditGuarantee.SupplierInput", "المورد");
            UiInstrumentation.Identify(_beneficiaryInput, "Dialog.EditGuarantee.BeneficiaryInput", "الجهة المستفيدة");
            UiInstrumentation.Identify(_bankInput, "Dialog.EditGuarantee.BankInput", "البنك");
            UiInstrumentation.Identify(_typeInput, "Dialog.EditGuarantee.TypeInput", "نوع الضمان");
            UiInstrumentation.Identify(_amountInput, "Dialog.EditGuarantee.AmountInput", "المبلغ");
            UiInstrumentation.Identify(_expiryInput, "Dialog.EditGuarantee.ExpiryInput", "تاريخ الانتهاء");
            UiInstrumentation.Identify(_referenceTypeInput, "Dialog.EditGuarantee.ReferenceTypeInput", "نوع المرجع");
            UiInstrumentation.Identify(_referenceNumberInput, "Dialog.EditGuarantee.ReferenceNumberInput", "رقم المرجع");
            UiInstrumentation.Identify(_notesInput, "Dialog.EditGuarantee.NotesInput", "ملاحظات");
            UiInstrumentation.Identify(_existingAttachmentsLabel, "Dialog.EditGuarantee.ExistingAttachmentsSummary", "ملخص المرفقات الحالية");
            UiInstrumentation.Identify(_newAttachmentTypeInput, "Dialog.EditGuarantee.AttachmentTypeInput", "نوع المستند");
            UiInstrumentation.Identify(_newAttachmentsLabel, "Dialog.EditGuarantee.NewAttachmentsSummary", "ملخص المرفقات الجديدة");
            UiInstrumentation.Identify(_consequenceSummary, "Dialog.EditGuarantee.ConsequenceSummary", "معاينة أثر الحفظ");
            UiInstrumentation.Identify(_consequencePrimary, "Dialog.EditGuarantee.ConsequencePrimary", "تفاصيل أثر الحفظ");
            UiInstrumentation.Identify(_consequenceSecondary, "Dialog.EditGuarantee.ConsequenceSecondary", "ملاحظات أثر الحفظ");
            Width = 560;
            Height = 760;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(EditGuaranteeDialog), TryAccept, "احفظ تعديل الضمان أو أغلق نافذته أولاً.");

            foreach (string bank in banks.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _bankInput.Items.Add(bank);
            }

            foreach (string guaranteeType in guaranteeTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                _typeInput.Items.Add(guaranteeType);
            }

            _guaranteeNoInput.Text = currentGuarantee.GuaranteeNo;
            _supplierInput.Text = currentGuarantee.Supplier;
            _beneficiaryInput.Text = BusinessPartyDefaults.NormalizeBeneficiary(currentGuarantee.Beneficiary);
            _beneficiaryInput.IsReadOnly = true;
            _bankInput.Text = currentGuarantee.Bank;
            _typeInput.Text = currentGuarantee.GuaranteeType;
            _amountInput.Text = currentGuarantee.Amount.ToString("N2", CultureInfo.InvariantCulture);
            _expiryInput.Text = currentGuarantee.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            _referenceNumberInput.Text = currentGuarantee.ReferenceNumber;
            _notesInput.Text = currentGuarantee.Notes;
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
            _referenceTypeInput.SelectedItem = ((IEnumerable<ReferenceTypeOption>)_referenceTypeInput.ItemsSource)
                .FirstOrDefault(item => item.Value == currentGuarantee.ReferenceType);
            if (_referenceTypeInput.SelectedItem == null)
            {
                _referenceTypeInput.SelectedIndex = 0;
            }
            _referenceTypeInput.Height = 34;
            _referenceTypeInput.Margin = new Thickness(0, 4, 0, 10);

            _newAttachmentTypeInput.ItemsSource = BuildAttachmentTypeOptions();
            _newAttachmentTypeInput.DisplayMemberPath = nameof(AttachmentDocumentTypeOption.Label);
            _newAttachmentTypeInput.SelectedIndex = 0;
            _newAttachmentTypeInput.Height = 30;
            _newAttachmentTypeInput.Width = 132;
            _newAttachmentTypeInput.Margin = new Thickness(0, 0, 8, 0);

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
            fields.Children.Add(BuildExistingAttachmentsSection());
            fields.Children.Add(BuildNewAttachmentsSection());

            scroll.Content = fields;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var saveButton = UiInstrumentation.Identify(
                new Button { Content = "حفظ التعديل", Width = 96, Height = 32, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) },
                "Dialog.EditGuarantee.SaveButton",
                "حفظ التعديل");
            saveButton.Click += (_, _) => TryAccept();
            var reportsButton = UiInstrumentation.Identify(
                new Button { Content = "التقارير", Width = 96, Height = 32, Margin = new Thickness(8, 0, 0, 0) },
                "Dialog.EditGuarantee.ReportsButton",
                "التقارير");
            reportsButton.Click += (_, _) => DialogFormSupport.RunWorkspaceReport(Title);
            var cancelButton = UiInstrumentation.Identify(
                new Button { Content = "إلغاء", Width = 90, Height = 32, IsCancel = true },
                "Dialog.EditGuarantee.CancelButton",
                "إلغاء");
            actions.Children.Add(saveButton);
            actions.Children.Add(reportsButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);
            root.Children.Add(scroll);

            Content = root;
            RefreshExistingAttachmentsState();
            RefreshNewAttachmentsState();
            WireDirtyTracking();
            RefreshConsequencePreview();
            Closing += OnClosing;
        }

        public static bool TryShow(
            Guarantee currentGuarantee,
            IEnumerable<string> banks,
            IEnumerable<string> guaranteeTypes,
            Func<string, bool> isGuaranteeNoUnique,
            out EditGuaranteeInput input)
        {
            var dialog = new EditGuaranteeDialog(currentGuarantee, banks, guaranteeTypes, isGuaranteeNoUnique)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true && dialog._input != null;
            input = dialog._input ?? new EditGuaranteeInput(
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
                new List<AttachmentInput>(),
                new List<AttachmentRecord>());
            return accepted;
        }

        private FrameworkElement BuildExistingAttachmentsSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            panel.Children.Add(BuildLabel("المرفقات الحالية"));
            _existingAttachmentsLabel.FontSize = 11;
            _existingAttachmentsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _existingAttachmentsLabel.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(_existingAttachmentsLabel);
            panel.Children.Add(_existingAttachmentsList);
            return panel;
        }

        private FrameworkElement BuildNewAttachmentsSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            panel.Children.Add(BuildLabel("مرفقات جديدة"));

            var attachmentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var attachmentButton = UiInstrumentation.Identify(
                new Button { Content = "إضافة مرفقات", Width = 112, Height = 30 },
                "Dialog.EditGuarantee.AttachmentsButton",
                "إضافة مرفقات");
            attachmentButton.Click += (_, _) => PickAttachments();

            _newAttachmentsLabel.FontSize = 11;
            _newAttachmentsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _newAttachmentsLabel.VerticalAlignment = VerticalAlignment.Center;
            _newAttachmentsLabel.Margin = new Thickness(10, 0, 0, 0);

            attachmentRow.Children.Add(_newAttachmentTypeInput);
            attachmentRow.Children.Add(attachmentButton);
            attachmentRow.Children.Add(_newAttachmentsLabel);
            panel.Children.Add(attachmentRow);
            panel.Children.Add(_newAttachmentsList);
            return panel;
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
                MessageBox.Show("أكمل رقم الضمان والمورد والبنك ونوع الضمان.", "تعديل الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isGuaranteeNoUnique(guaranteeNo))
            {
                MessageBox.Show("رقم الضمان موجود مسبقاً.", "تعديل الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("أدخل مبلغاً صحيحاً أكبر من صفر.", "تعديل الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate))
            {
                MessageBox.Show("صيغة تاريخ الانتهاء غير صحيحة. استخدم مثلاً 2026/12/31.", "تعديل الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GuaranteeReferenceType referenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;
            List<AttachmentRecord> removedAttachments = _currentGuarantee.Attachments
                .Where(item => _removedAttachmentIds.Contains(item.Id))
                .ToList();

            _input = new EditGuaranteeInput(
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
                [.. _newAttachments],
                removedAttachments);
            _allowCloseWithoutPrompt = true;
            DialogResult = true;
        }

        private void PickAttachments()
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختيار مرفقات إضافية",
                Filter = "ملفات المستندات|*.pdf;*.doc;*.docx;*.png;*.jpg;*.jpeg|كل الملفات|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            AttachmentDocumentType selectedType = (_newAttachmentTypeInput.SelectedItem as AttachmentDocumentTypeOption)?.Value
                ?? AttachmentDocumentType.SupportingDocument;

            foreach (string file in dialog.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_newAttachments.Any(existing => string.Equals(existing.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                {
                    _newAttachments.Add(new AttachmentInput(file, selectedType));
                }
            }

            RefreshNewAttachmentsState();
            MarkDirty();
        }

        private void RefreshExistingAttachmentsState()
        {
            _existingAttachmentsList.Children.Clear();

            List<AttachmentRecord> attachments = _currentGuarantee.Attachments ?? new List<AttachmentRecord>();
            int keptCount = attachments.Count(item => !_removedAttachmentIds.Contains(item.Id));
            int removedCount = attachments.Count - keptCount;

            _existingAttachmentsLabel.Text = attachments.Count == 0
                ? "لا توجد مرفقات محفوظة"
                : removedCount == 0
                    ? $"{keptCount.ToString("N0", CultureInfo.InvariantCulture)} مرفق محفوظ"
                    : $"{keptCount.ToString("N0", CultureInfo.InvariantCulture)} محفوظ | {removedCount.ToString("N0", CultureInfo.InvariantCulture)} سيُزال";

            if (attachments.Count == 0)
            {
                return;
            }

            foreach (AttachmentRecord attachment in attachments)
            {
                bool removed = _removedAttachmentIds.Contains(attachment.Id);
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6), FlowDirection = FlowDirection.LeftToRight };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock
                {
                    Text = $"{attachment.OriginalFileName} • {attachment.DocumentTypeLabel}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(removed ? "#94A3B8" : "#0F172A")),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FlowDirection = FlowDirection.LeftToRight
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var openButton = new Button
                {
                    Content = "فتح",
                    Width = 56,
                    Height = 28,
                    FontSize = 10
                };
                openButton.Click += (_, _) => OpenExistingAttachment(attachment);
                Grid.SetColumn(openButton, 1);
                row.Children.Add(openButton);

                var toggleButton = new Button
                {
                    Content = removed ? "استعادة" : "إزالة",
                    Width = 68,
                    Height = 28,
                    FontSize = 10,
                    Background = removed
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3")),
                    BorderBrush = removed
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFDBFE"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7C5C5")),
                    Foreground = removed
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                };
                toggleButton.Click += (_, _) =>
                {
                    if (!_removedAttachmentIds.Add(attachment.Id))
                    {
                        _removedAttachmentIds.Remove(attachment.Id);
                    }

                    RefreshExistingAttachmentsState();
                    MarkDirty();
                };
                Grid.SetColumn(toggleButton, 3);
                row.Children.Add(toggleButton);

                _existingAttachmentsList.Children.Add(row);
            }
        }

        private void RefreshNewAttachmentsState()
        {
            _newAttachmentsList.Children.Clear();
            _newAttachmentsLabel.Text = _newAttachments.Count == 0
                ? "بدون مرفقات جديدة"
                : $"{_newAttachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق جديد";

            foreach (AttachmentInput attachment in _newAttachments)
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
                    _newAttachments.Remove(attachment);
                    RefreshNewAttachmentsState();
                    MarkDirty();
                };
                Grid.SetColumn(removeButton, 3);
                row.Children.Add(removeButton);

                _newAttachmentsList.Children.Add(row);
            }
        }

        private static void OpenExistingAttachment(AttachmentRecord attachment)
        {
            if (!attachment.Exists)
            {
                MessageBox.Show("تعذر فتح المرفق. الملف غير موجود.", "المرفقات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo(attachment.FilePath) { UseShellExecute = true });
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
                _newAttachmentTypeInput,
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
            string currentAmountText = _currentGuarantee.Amount.ToString("N2", CultureInfo.InvariantCulture).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string currentExpiryText = _currentGuarantee.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            string referenceNumber = _referenceNumberInput.Text.Trim();
            string notes = _notesInput.Text.Trim();
            GuaranteeReferenceType referenceType = (_referenceTypeInput.SelectedItem as ReferenceTypeOption)?.Value ?? GuaranteeReferenceType.None;
            string effectiveBeneficiary = BusinessPartyDefaults.NormalizeBeneficiary(beneficiary);
            string currentBeneficiary = BusinessPartyDefaults.NormalizeBeneficiary(_currentGuarantee.Beneficiary);
            string currentNotes = _currentGuarantee.Notes?.Trim() ?? string.Empty;

            return !string.Equals(guaranteeNo, _currentGuarantee.GuaranteeNo, StringComparison.Ordinal)
                   || !string.Equals(supplier, _currentGuarantee.Supplier, StringComparison.Ordinal)
                   || !string.Equals(effectiveBeneficiary, currentBeneficiary, StringComparison.Ordinal)
                   || !string.Equals(bank, _currentGuarantee.Bank, StringComparison.Ordinal)
                   || !string.Equals(guaranteeType, _currentGuarantee.GuaranteeType, StringComparison.Ordinal)
                   || !string.Equals(amountText, currentAmountText, StringComparison.Ordinal)
                   || !string.Equals(expiryText, currentExpiryText, StringComparison.Ordinal)
                   || referenceType != _currentGuarantee.ReferenceType
                   || !string.Equals(referenceNumber, _currentGuarantee.ReferenceNumber, StringComparison.Ordinal)
                   || !string.Equals(notes, currentNotes, StringComparison.Ordinal)
                   || _newAttachments.Count > 0
                   || _removedAttachmentIds.Count > 0;
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
            string notes = _notesInput.Text.Trim();
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
                    blockers.Count > 1 ? string.Join(" ", blockers.Skip(1)) : "لن يُنشأ إصدار جديد حتى تصبح القيم صالحة.",
                    warningState: true);
                return;
            }

            string effectiveBeneficiary = BusinessPartyDefaults.NormalizeBeneficiary(beneficiary);
            string currentBeneficiary = BusinessPartyDefaults.NormalizeBeneficiary(_currentGuarantee.Beneficiary);

            var changeLabels = new List<string>();
            if (!string.Equals(guaranteeNo, _currentGuarantee.GuaranteeNo, StringComparison.Ordinal))
            {
                changeLabels.Add("رقم الضمان");
            }

            if (!string.Equals(supplier, _currentGuarantee.Supplier, StringComparison.Ordinal))
            {
                changeLabels.Add("المورد");
            }

            if (!string.Equals(effectiveBeneficiary, currentBeneficiary, StringComparison.Ordinal))
            {
                changeLabels.Add("الجهة المستفيدة");
            }

            if (!string.Equals(bank, _currentGuarantee.Bank, StringComparison.Ordinal))
            {
                changeLabels.Add("البنك");
            }

            if (!string.Equals(guaranteeType, _currentGuarantee.GuaranteeType, StringComparison.Ordinal))
            {
                changeLabels.Add("نوع الضمان");
            }

            if (amount != _currentGuarantee.Amount)
            {
                changeLabels.Add("المبلغ");
            }

            if (expiryDate.Date != _currentGuarantee.ExpiryDate.Date)
            {
                changeLabels.Add("تاريخ الانتهاء");
            }

            if (referenceType != _currentGuarantee.ReferenceType || !string.Equals(referenceNumber, _currentGuarantee.ReferenceNumber, StringComparison.Ordinal))
            {
                changeLabels.Add("المرجع");
            }

            if (!string.Equals(notes, _currentGuarantee.Notes?.Trim() ?? string.Empty, StringComparison.Ordinal))
            {
                changeLabels.Add("الملاحظات");
            }

            int addedAttachments = _newAttachments.Count;
            int removedAttachments = _removedAttachmentIds.Count;
            if (addedAttachments > 0 || removedAttachments > 0)
            {
                changeLabels.Add("المرفقات");
            }

            if (changeLabels.Count == 0)
            {
                SetConsequenceState(
                    "لا توجد تغييرات جديدة ستُحفظ الآن.",
                    $"الإصدار الحالي {(_currentGuarantee.VersionLabel)} سيبقى كما هو.",
                    "غيّر حقلًا واحدًا على الأقل أو أضف/أزل مرفقًا قبل حفظ التعديل.",
                    warningState: false,
                    neutralState: true);
                return;
            }

            string changedFieldsSummary = changeLabels.Count <= 3
                ? string.Join("، ", changeLabels)
                : $"{string.Join("، ", changeLabels.Take(3))} + {changeLabels.Count - 3} أخرى";
            string attachmentSummary = addedAttachments == 0 && removedAttachments == 0
                ? "المرفقات ستبقى كما هي"
                : $"المرفقات: +{addedAttachments.ToString("N0", CultureInfo.InvariantCulture)} / -{removedAttachments.ToString("N0", CultureInfo.InvariantCulture)}";

            SetConsequenceState(
                $"سيُنشأ الإصدار {GuaranteeVersionDisplay.GetLabel(_currentGuarantee.VersionNumber + 1)} لهذا الضمان.",
                $"أبرز التغييرات: {changedFieldsSummary}.",
                $"{attachmentSummary} • الإصدار الحالي {_currentGuarantee.VersionLabel} سيبقى محفوظًا في السجل.",
                warningState: false,
                neutralState: false);
        }

        private void SetConsequenceState(string summary, string primary, string secondary, bool warningState, bool neutralState = false)
        {
            ApplyConsequenceText(_consequenceSummary, summary);
            ApplyConsequenceText(_consequencePrimary, primary);
            ApplyConsequenceText(_consequenceSecondary, secondary);

            string background = neutralState ? "#F8FAFC" : warningState ? "#FFF9EC" : "#EFF6FF";
            string border = neutralState ? "#D8E1EE" : warningState ? "#F6DE99" : "#BFDBFE";
            string summaryColor = neutralState ? "#64748B" : warningState ? "#E09408" : "#1D4ED8";

            _consequenceCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
            _consequenceCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
            _consequenceSummary.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(summaryColor));
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

        private static List<AttachmentDocumentTypeOption> BuildAttachmentTypeOptions()
        {
            return AttachmentDocumentTypeText.OfficialAttachmentTypes
                .Select(type => new AttachmentDocumentTypeOption(type, AttachmentDocumentTypeText.Label(type)))
                .ToList();
        }

        private sealed record ReferenceTypeOption(GuaranteeReferenceType Value, string Label);
    }
}
