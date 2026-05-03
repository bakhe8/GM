using System;
using System.Collections.Generic;
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
    public sealed record ReplacementRequestInput(
        string ReplacementGuaranteeNo,
        string ReplacementSupplier,
        string ReplacementBank,
        decimal ReplacementAmount,
        DateTime ReplacementExpiryDate,
        GuaranteeDateCalendar ReplacementDateCalendar,
        string ReplacementGuaranteeType,
        string ReplacementBeneficiary,
        GuaranteeReferenceType ReplacementReferenceType,
        string ReplacementReferenceNumber,
        string Notes,
        List<AttachmentInput> Attachments);

    public sealed class ReplacementRequestDialog : Window
    {
        private readonly Func<string, bool> _isGuaranteeNoUnique;
        private readonly Guarantee _currentGuarantee;
        private readonly TextBox _guaranteeNoInput = BuildTextBox();
        private readonly ComboBox _bankInput = BuildEditableComboBox();
        private readonly TextBox _amountInput = BuildTextBox();
        private readonly TextBox _expiryInput = BuildTextBox();
        private readonly ComboBox _typeInput = BuildEditableComboBox();
        private readonly TextBox _notesInput = BuildTextBox();
        private readonly ComboBox _attachmentTypeInput = new();
        private readonly TextBlock _attachmentsLabel = new();
        private readonly StackPanel _attachmentsList = new() { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<AttachmentInput> _attachments = new();

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
            Height = 610;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
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

            _bankInput.Text = currentGuarantee.Bank;
            _amountInput.Text = currentGuarantee.Amount.ToString("N2", CultureInfo.InvariantCulture);
            _expiryInput.Text = DualCalendarDateService.FormatDate(currentGuarantee.ExpiryDate, currentGuarantee.DateCalendar);
            _typeInput.Text = currentGuarantee.GuaranteeType;
            _notesInput.Text = $"استبدال للضمان {currentGuarantee.GuaranteeNo}";
            _notesInput.Height = 58;
            _notesInput.AcceptsReturn = true;
            _notesInput.TextWrapping = TextWrapping.Wrap;

            _attachmentTypeInput.ItemsSource = BuildAttachmentTypeOptions();
            _attachmentTypeInput.DisplayMemberPath = nameof(AttachmentDocumentTypeOption.Label);
            _attachmentTypeInput.SelectedIndex = 0;
            _attachmentTypeInput.Height = 30;
            _attachmentTypeInput.Width = 132;
            _attachmentTypeInput.Margin = new Thickness(0, 0, 8, 0);

            var root = new DockPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fields = new StackPanel();

            fields.Children.Add(BuildTwoColumnRow(
                BuildField("رقم الضمان البديل", _guaranteeNoInput),
                BuildField("رقم الضمان الحالي", BuildReadOnlyValue(currentGuarantee.GuaranteeNo))));
            fields.Children.Add(BuildTwoColumnRow(
                BuildField("البنك البديل", _bankInput),
                BuildField("البنك الحالي", BuildReadOnlyValue(currentGuarantee.Bank))));
            fields.Children.Add(BuildTwoColumnRow(
                BuildField("المبلغ البديل", _amountInput),
                BuildField("المبلغ الحالي", BuildReadOnlyValue(currentGuarantee.Amount.ToString("N2", CultureInfo.InvariantCulture)))));
            fields.Children.Add(BuildTwoColumnRow(
                BuildField("نوع الضمان البديل", _typeInput),
                BuildField("نوع الضمان الحالي", BuildReadOnlyValue(currentGuarantee.GuaranteeType))));
            fields.Children.Add(BuildTwoColumnRow(
                BuildField("تاريخ الانتهاء البديل", _expiryInput),
                BuildField("تاريخ الانتهاء الحالي", BuildReadOnlyValue(DualCalendarDateService.FormatDate(currentGuarantee.ExpiryDate, currentGuarantee.DateCalendar)))));
            fields.Children.Add(BuildField("ملاحظات", _notesInput));
            fields.Children.Add(BuildAttachmentsSection());

            scroll.Content = fields;

            var saveButton = new Button { Content = "حفظ", IsDefault = true };
            saveButton.Click += (_, _) => TryAccept();
            var cancelButton = new Button { Content = "إلغاء", IsCancel = true };
            var actions = DialogFormSupport.BuildActionBar(saveButton, cancelButton, 96, 96);
            DockPanel.SetDock(actions, Dock.Bottom);
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
                currentGuarantee.DateCalendar,
                string.Empty,
                string.Empty,
                GuaranteeReferenceType.None,
                string.Empty,
                string.Empty,
                new List<AttachmentInput>());
            return accepted;
        }

        private FrameworkElement BuildAttachmentsSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            panel.Children.Add(BuildLabel("مرفقات الضمان البديل"));

            var attachmentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var attachmentButton = new Button { Content = "إضافة مرفقات", Width = 112, Height = 30 };
            attachmentButton.Click += (_, _) => PickAttachments();

            _attachmentsLabel.Text = "بدون مرفقات";
            _attachmentsLabel.FontSize = 11;
            _attachmentsLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _attachmentsLabel.VerticalAlignment = VerticalAlignment.Center;
            _attachmentsLabel.Margin = new Thickness(10, 0, 0, 0);

            attachmentRow.Children.Add(_attachmentTypeInput);
            attachmentRow.Children.Add(attachmentButton);
            attachmentRow.Children.Add(_attachmentsLabel);
            panel.Children.Add(attachmentRow);
            panel.Children.Add(_attachmentsList);
            return panel;
        }

        private void PickAttachments()
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختيار مرفقات الضمان البديل",
                Filter = "ملفات المستندات|*.pdf;*.doc;*.docx;*.png;*.jpg;*.jpeg|كل الملفات|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            AttachmentDocumentType selectedType = (_attachmentTypeInput.SelectedItem as AttachmentDocumentTypeOption)?.Value
                ?? AttachmentDocumentType.GuaranteeImage;

            foreach (string file in dialog.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_attachments.Any(existing => string.Equals(existing.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                {
                    _attachments.Add(new AttachmentInput(file, selectedType));
                }
            }

            RefreshAttachmentsState();
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
                    FontWeight = FontWeights.Medium,
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
                };
                Grid.SetColumn(removeButton, 3);
                row.Children.Add(removeButton);

                _attachmentsList.Children.Add(row);
            }
        }

        private void TryAccept()
        {
            string replacementGuaranteeNo = _guaranteeNoInput.Text.Trim();
            string replacementBank = GetComboText(_bankInput);
            string amountText = _amountInput.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            string expiryText = _expiryInput.Text.Trim();
            string replacementGuaranteeType = GetComboText(_typeInput);
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

            if (string.IsNullOrWhiteSpace(replacementBank)
                || string.IsNullOrWhiteSpace(replacementGuaranteeType))
            {
                MessageBox.Show("أكمل البنك ونوع الضمان البديل.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ArabicAmountFormatter.TryParsePositiveSaudiRiyalAmount(amountText, out decimal replacementAmount))
            {
                MessageBox.Show("أدخل مبلغًا صحيحًا أكبر من صفر، وبحد أقصى خانتين للهلل.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DualCalendarDateService.TryParseDate(expiryText, out DateTime replacementExpiryDate, out GuaranteeDateCalendar replacementDateCalendar))
            {
                MessageBox.Show($"صيغة تاريخ انتهاء الضمان البديل غير صحيحة. استخدم مثلاً {DualCalendarDateService.InputExamples}.", "طلب استبدال", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _input = new ReplacementRequestInput(
                replacementGuaranteeNo,
                _currentGuarantee.Supplier,
                replacementBank,
                replacementAmount,
                replacementExpiryDate.Date,
                replacementDateCalendar,
                replacementGuaranteeType,
                BusinessPartyDefaults.NormalizeBeneficiary(_currentGuarantee.Beneficiary),
                _currentGuarantee.ReferenceType,
                _currentGuarantee.ReferenceNumber,
                string.IsNullOrWhiteSpace(notes) ? $"استبدال للضمان {_currentGuarantee.GuaranteeNo}" : notes,
                [.. _attachments]);
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
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                    FlowDirection = FlowDirection.LeftToRight,
                    TextAlignment = TextAlignment.Right,
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

        private static FrameworkElement BuildTwoColumnRow(FrameworkElement firstField, FrameworkElement secondField)
        {
            var row = new Grid { FlowDirection = FlowDirection.RightToLeft };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(firstField, 0);
            Grid.SetColumn(secondField, 2);
            row.Children.Add(firstField);
            row.Children.Add(secondField);
            return row;
        }

        private static TextBlock BuildLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
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

        private static List<AttachmentDocumentTypeOption> BuildAttachmentTypeOptions()
        {
            return AttachmentDocumentTypeText.OfficialAttachmentTypes
                .Select(type => new AttachmentDocumentTypeOption(type, AttachmentDocumentTypeText.Label(type)))
                .ToList();
        }

    }
}
