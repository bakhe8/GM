using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    internal static class DialogFormSupport
    {
        public static void WireDirtyTracking(Action markDirty, params FrameworkElement[] elements)
        {
            foreach (FrameworkElement element in elements)
            {
                switch (element)
                {
                    case TextBox textBox:
                        textBox.TextChanged += (_, _) => markDirty();
                        break;
                    case ComboBox comboBox:
                        comboBox.SelectionChanged += (_, _) => markDirty();
                        if (comboBox.IsEditable)
                        {
                            comboBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => markDirty()));
                        }
                        break;
                }
            }
        }

        public static bool ConfirmDiscardChanges()
        {
            return App.CurrentApp.GetRequiredService<IAppDialogService>().Confirm(
                "لديك تعديلات غير محفوظة. هل تريد إغلاق النافذة وفقدان هذه التعديلات؟",
                "تأكيد الإغلاق");
        }

        public static void RunWorkspaceReport(string ownerTitle)
        {
            if (!ReportPickerDialog.TryShow(out string reportKey))
            {
                return;
            }

            IDatabaseService database = App.CurrentApp.GetRequiredService<IDatabaseService>();
            IExcelService excel = App.CurrentApp.GetRequiredService<IExcelService>();

            bool exported = WorkspaceReportCatalog.Run(reportKey, database, excel);
            string reportTitle = WorkspaceReportCatalog.PortfolioActions
                .Concat(WorkspaceReportCatalog.OperationalActions)
                .FirstOrDefault(action => action.Key == reportKey)?.Title ?? "التقرير";

            IAppDialogService dialogs = App.CurrentApp.GetRequiredService<IAppDialogService>();
            if (exported)
            {
                dialogs.ShowInformation(
                    $"تم إنشاء تقرير {reportTitle} من البيانات المحفوظة الحالية.",
                    ownerTitle);
            }
            else
            {
                dialogs.ShowWarning(
                    $"تم إلغاء إنشاء تقرير {reportTitle}.",
                    ownerTitle);
            }
        }
    }

    public sealed class PromptDialog : Window
    {
        private readonly TextBox _input;

        private PromptDialog(string title, string label, string defaultValue)
        {
            Title = title;
            Width = 360;
            Height = 174;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(PromptDialog), () => DialogResult = true, "أغلق نافذة الإدخال الحالية أو أكملها أولاً.");

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
                Margin = new Thickness(0, 0, 0, 8)
            });

            _input = new TextBox
            {
                Text = defaultValue,
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(_input, 1);
            root.Children.Add(_input);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var okButton = new Button
            {
                Content = "موافق",
                Width = 88,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            okButton.Click += (_, _) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 88,
                Height = 32,
                IsCancel = true
            };

            actions.Children.Add(okButton);
            actions.Children.Add(cancelButton);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            Content = root;
        }

        public static bool TryShow(string title, string label, string defaultValue, out string value)
        {
            var dialog = new PromptDialog(title, label, defaultValue)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true;
            value = accepted ? dialog._input.Text : string.Empty;
            return accepted;
        }
    }

    public sealed class BankResponseDialog : Window
    {
        private readonly ComboBox _requestSelector;
        private readonly ComboBox _statusSelector;
        private readonly TextBox _notesInput;
        private readonly TextBlock _documentLabel;
        private string _responseDocumentPath = string.Empty;

        private BankResponseDialog(IReadOnlyList<WorkflowRequest> requests)
        {
            Title = "تسجيل رد البنك";
            Width = 460;
            Height = 368;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(BankResponseDialog), () => DialogResult = true, "أكمل تسجيل رد البنك أو أغلق النافذة الحالية أولاً.");

            var requestOptions = requests
                .Select(request => new BankResponseRequestOption(request))
                .ToList();

            var statusOptions = new List<BankResponseStatusOption>
            {
                new(RequestStatus.Executed, "منفذ"),
                new(RequestStatus.Rejected, "مرفوض"),
                new(RequestStatus.Cancelled, "ملغى")
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildLabel("الطلب المعلق"));

            _requestSelector = new ComboBox
            {
                ItemsSource = requestOptions,
                SelectedIndex = 0,
                DisplayMemberPath = nameof(BankResponseRequestOption.Display),
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 7, 0, 12)
            };
            UiInstrumentation.Identify(this, "Dialog.BankResponse", Title);
            UiInstrumentation.Identify(_requestSelector, "Dialog.BankResponse.RequestSelector", "الطلب المعلق");
            Grid.SetRow(_requestSelector, 1);
            root.Children.Add(_requestSelector);

            var statusLabel = BuildLabel("نتيجة الرد");
            Grid.SetRow(statusLabel, 2);
            root.Children.Add(statusLabel);

            _statusSelector = new ComboBox
            {
                ItemsSource = statusOptions,
                SelectedIndex = 0,
                DisplayMemberPath = nameof(BankResponseStatusOption.Label),
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 7, 0, 12)
            };
            UiInstrumentation.Identify(_statusSelector, "Dialog.BankResponse.StatusSelector", "نتيجة الرد");
            Grid.SetRow(_statusSelector, 3);
            root.Children.Add(_statusSelector);

            var notesLabel = BuildLabel("ملاحظات الرد");
            Grid.SetRow(notesLabel, 4);
            root.Children.Add(notesLabel);

            _notesInput = new TextBox
            {
                Text = "تم استلام رد البنك.",
                Height = 54,
                FontSize = 12,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
            UiInstrumentation.Identify(_notesInput, "Dialog.BankResponse.NotesInput", "ملاحظات الرد");
            Grid.SetRow(_notesInput, 5);
            root.Children.Add(_notesInput);

            var documentRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            documentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            documentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chooseDocumentButton = new Button
            {
                Content = "اختيار مستند",
                Width = 104,
                Height = 30
            };
            UiInstrumentation.Identify(chooseDocumentButton, "Dialog.BankResponse.ChooseDocumentButton", "اختيار مستند رد البنك");
            chooseDocumentButton.Click += (_, _) => ChooseResponseDocument();
            documentRow.Children.Add(chooseDocumentButton);

            _documentLabel = new TextBlock
            {
                Text = "بدون مستند",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(10, 0, 0, 0)
            };
            UiInstrumentation.Identify(_documentLabel, "Dialog.BankResponse.DocumentSummary", "ملخص مستند رد البنك");
            Grid.SetColumn(_documentLabel, 1);
            documentRow.Children.Add(_documentLabel);

            Grid.SetRow(documentRow, 6);
            root.Children.Add(documentRow);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var okButton = new Button
            {
                Content = "تسجيل",
                Width = 90,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            UiInstrumentation.Identify(okButton, "Dialog.BankResponse.SaveButton", "اعتماد رد البنك");
            okButton.Click += (_, _) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 90,
                Height = 32,
                IsCancel = true
            };
            UiInstrumentation.Identify(cancelButton, "Dialog.BankResponse.CancelButton", "إلغاء تسجيل رد البنك");

            actions.Children.Add(okButton);
            actions.Children.Add(cancelButton);
            Grid.SetRow(actions, 7);
            root.Children.Add(actions);

            Content = root;
        }

        public static bool TryShow(
            IReadOnlyList<WorkflowRequest> requests,
            out int requestId,
            out RequestStatus resultStatus,
            out string notes,
            out string responseDocumentPath)
        {
            var dialog = new BankResponseDialog(requests)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true;
            if (!accepted
                || dialog._requestSelector.SelectedItem is not BankResponseRequestOption requestOption
                || dialog._statusSelector.SelectedItem is not BankResponseStatusOption statusOption)
            {
                requestId = 0;
                resultStatus = RequestStatus.Pending;
                notes = string.Empty;
                responseDocumentPath = string.Empty;
                return false;
            }

            requestId = requestOption.Request.Id;
            resultStatus = statusOption.Status;
            notes = dialog._notesInput.Text.Trim();
            responseDocumentPath = dialog._responseDocumentPath;
            return true;
        }

        private void ChooseResponseDocument()
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختيار مستند رد البنك",
                Filter = "ملفات المستندات|*.pdf;*.doc;*.docx;*.png;*.jpg;*.jpeg|كل الملفات|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _responseDocumentPath = dialog.FileName;
            _documentLabel.Text = Path.GetFileName(dialog.FileName);
        }

        private static TextBlock BuildLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"))
            };
        }

        private sealed class BankResponseRequestOption
        {
            public BankResponseRequestOption(WorkflowRequest request)
            {
                Request = request;
                Display = $"{request.TypeLabel} - {request.RequestDate:yyyy/MM/dd} - {request.RequestedValueLabel}";
            }

            public WorkflowRequest Request { get; }
            public string Display { get; }
        }

        private sealed class BankResponseStatusOption
        {
            public BankResponseStatusOption(RequestStatus status, string label)
            {
                Status = status;
                Label = label;
            }

            public RequestStatus Status { get; }
            public string Label { get; }
        }
    }

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
        List<string> AttachmentPaths);

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
        List<string> NewAttachmentPaths,
        List<AttachmentRecord> RemovedAttachments);

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
        private readonly TextBlock _attachmentsLabel = new();
        private readonly List<string> _attachmentPaths = new();
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
            UiInstrumentation.Identify(_beneficiaryInput, "Dialog.NewGuarantee.BeneficiaryInput", "المستفيد");
            UiInstrumentation.Identify(_bankInput, "Dialog.NewGuarantee.BankInput", "البنك");
            UiInstrumentation.Identify(_typeInput, "Dialog.NewGuarantee.TypeInput", "نوع الضمان");
            UiInstrumentation.Identify(_amountInput, "Dialog.NewGuarantee.AmountInput", "المبلغ");
            UiInstrumentation.Identify(_expiryInput, "Dialog.NewGuarantee.ExpiryInput", "تاريخ الانتهاء");
            UiInstrumentation.Identify(_referenceTypeInput, "Dialog.NewGuarantee.ReferenceTypeInput", "نوع المرجع");
            UiInstrumentation.Identify(_referenceNumberInput, "Dialog.NewGuarantee.ReferenceNumberInput", "رقم المرجع");
            UiInstrumentation.Identify(_notesInput, "Dialog.NewGuarantee.NotesInput", "ملاحظات");
            UiInstrumentation.Identify(_attachmentsLabel, "Dialog.NewGuarantee.AttachmentsSummary", "ملخص المرفقات");
            UiInstrumentation.Identify(_consequenceSummary, "Dialog.NewGuarantee.ConsequenceSummary", "معاينة أثر الحفظ");
            UiInstrumentation.Identify(_consequencePrimary, "Dialog.NewGuarantee.ConsequencePrimary", "تفاصيل أثر الحفظ");
            UiInstrumentation.Identify(_consequenceSecondary, "Dialog.NewGuarantee.ConsequenceSecondary", "ملاحظات أثر الحفظ");
            Width = 520;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
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

            var root = new DockPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fields = new StackPanel();

            fields.Children.Add(BuildField("رقم الضمان", _guaranteeNoInput));
            fields.Children.Add(BuildField("المورد", _supplierInput));
            fields.Children.Add(BuildField("المستفيد", _beneficiaryInput));
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
            attachmentRow.Children.Add(attachmentButton);
            attachmentRow.Children.Add(_attachmentsLabel);
            attachmentPanel.Children.Add(attachmentRow);
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
                new List<string>());
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
                string.IsNullOrWhiteSpace(beneficiary) ? supplier : beneficiary,
                bank,
                guaranteeType,
                amount,
                expiryDate,
                referenceType,
                referenceNumber,
                notes,
                [.. _attachmentPaths]);
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

            _attachmentPaths.Clear();
            _attachmentPaths.AddRange(dialog.FileNames);
            _attachmentsLabel.Text = _attachmentPaths.Count == 0
                ? "بدون مرفقات"
                : $"{_attachmentPaths.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق";
            MarkDirty();
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
            if (_allowCloseWithoutPrompt || !_isDirty)
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"))
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
            ConfigureConsequenceText(_consequencePrimary, 11, FontWeights.Normal, "#334155");
            ConfigureConsequenceText(_consequenceSecondary, 10.8, FontWeights.Normal, "#64748B");
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

            string effectiveBeneficiary = string.IsNullOrWhiteSpace(beneficiary) ? supplier : beneficiary;
            string referenceSummary = BuildReferenceSummary(referenceType, referenceNumber);
            string attachmentSummary = _attachmentPaths.Count == 0
                ? "بدون مرفقات إضافية"
                : $"{_attachmentPaths.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق سيُحفظ";
            string beneficiarySummary = string.IsNullOrWhiteSpace(beneficiary)
                ? $"سيُستخدم المورد كمستفيد: {effectiveBeneficiary}"
                : $"المستفيد: {effectiveBeneficiary}";

            SetConsequenceState(
                "سيُنشأ ضمان جديد كإصدار أول (v1).",
                $"{guaranteeNo} • {bank} • {guaranteeType} • {amount.ToString("N2", CultureInfo.InvariantCulture)} ريال • {expiryDate:yyyy/MM/dd}",
                $"{beneficiarySummary} • {referenceSummary} • {attachmentSummary}",
                warningState: false);
        }

        private void SetConsequenceState(string summary, string primary, string secondary, bool warningState)
        {
            ApplyConsequenceText(_consequenceSummary, summary);
            ApplyConsequenceText(_consequencePrimary, primary);
            ApplyConsequenceText(_consequenceSecondary, secondary);

            _consequenceCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#FFFBEB" : "#EFF6FF"));
            _consequenceCard.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#FCD34D" : "#BFDBFE"));
            _consequenceSummary.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warningState ? "#92400E" : "#1D4ED8"));
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

        private sealed record ReferenceTypeOption(GuaranteeReferenceType Value, string Label);
    }

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
        private readonly TextBlock _newAttachmentsLabel = new();
        private readonly StackPanel _newAttachmentsList = new() { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<string> _newAttachmentPaths = new();
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
            UiInstrumentation.Identify(_beneficiaryInput, "Dialog.EditGuarantee.BeneficiaryInput", "المستفيد");
            UiInstrumentation.Identify(_bankInput, "Dialog.EditGuarantee.BankInput", "البنك");
            UiInstrumentation.Identify(_typeInput, "Dialog.EditGuarantee.TypeInput", "نوع الضمان");
            UiInstrumentation.Identify(_amountInput, "Dialog.EditGuarantee.AmountInput", "المبلغ");
            UiInstrumentation.Identify(_expiryInput, "Dialog.EditGuarantee.ExpiryInput", "تاريخ الانتهاء");
            UiInstrumentation.Identify(_referenceTypeInput, "Dialog.EditGuarantee.ReferenceTypeInput", "نوع المرجع");
            UiInstrumentation.Identify(_referenceNumberInput, "Dialog.EditGuarantee.ReferenceNumberInput", "رقم المرجع");
            UiInstrumentation.Identify(_notesInput, "Dialog.EditGuarantee.NotesInput", "ملاحظات");
            UiInstrumentation.Identify(_existingAttachmentsLabel, "Dialog.EditGuarantee.ExistingAttachmentsSummary", "ملخص المرفقات الحالية");
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
            FontFamily = new FontFamily("Segoe UI, Tahoma");
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
            _beneficiaryInput.Text = string.IsNullOrWhiteSpace(currentGuarantee.Beneficiary) ? currentGuarantee.Supplier : currentGuarantee.Beneficiary;
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

            var root = new DockPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var fields = new StackPanel();

            fields.Children.Add(BuildField("رقم الضمان", _guaranteeNoInput));
            fields.Children.Add(BuildField("المورد", _supplierInput));
            fields.Children.Add(BuildField("المستفيد", _beneficiaryInput));
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
                new List<string>(),
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
                string.IsNullOrWhiteSpace(beneficiary) ? supplier : beneficiary,
                bank,
                guaranteeType,
                amount,
                expiryDate,
                referenceType,
                referenceNumber,
                notes,
                [.. _newAttachmentPaths],
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

            foreach (string file in dialog.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_newAttachmentPaths.Any(existing => string.Equals(existing, file, StringComparison.OrdinalIgnoreCase)))
                {
                    _newAttachmentPaths.Add(file);
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
                    Text = attachment.OriginalFileName,
                    FontSize = 10.8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(removed ? "#94A3B8" : "#1F2937")),
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
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2")),
                    BorderBrush = removed
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFDBFE"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA")),
                    Foreground = removed
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
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
            _newAttachmentsLabel.Text = _newAttachmentPaths.Count == 0
                ? "بدون مرفقات جديدة"
                : $"{_newAttachmentPaths.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق جديد";

            foreach (string path in _newAttachmentPaths)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6), FlowDirection = FlowDirection.LeftToRight };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock
                {
                    Text = Path.GetFileName(path),
                    FontSize = 10.8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FlowDirection = FlowDirection.LeftToRight
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);

                var removeButton = new Button
                {
                    Content = "إزالة",
                    Width = 68,
                    Height = 28,
                    FontSize = 10,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                };
                removeButton.Click += (_, _) =>
                {
                    _newAttachmentPaths.Remove(path);
                    RefreshNewAttachmentsState();
                    MarkDirty();
                };
                Grid.SetColumn(removeButton, 1);
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
            if (_allowCloseWithoutPrompt || !_isDirty)
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"))
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
            ConfigureConsequenceText(_consequencePrimary, 11, FontWeights.Normal, "#334155");
            ConfigureConsequenceText(_consequenceSecondary, 10.8, FontWeights.Normal, "#64748B");
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

            string effectiveBeneficiary = string.IsNullOrWhiteSpace(beneficiary) ? supplier : beneficiary;
            string currentBeneficiary = string.IsNullOrWhiteSpace(_currentGuarantee.Beneficiary) ? _currentGuarantee.Supplier : _currentGuarantee.Beneficiary;

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
                changeLabels.Add("المستفيد");
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

            int addedAttachments = _newAttachmentPaths.Count;
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
                $"سيُنشأ إصدار جديد v{_currentGuarantee.VersionNumber + 1} لهذا الضمان.",
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

            string background = neutralState ? "#F8FAFC" : warningState ? "#FFFBEB" : "#EFF6FF";
            string border = neutralState ? "#D8E1EE" : warningState ? "#FCD34D" : "#BFDBFE";
            string summaryColor = neutralState ? "#334155" : warningState ? "#92400E" : "#1D4ED8";

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

        private sealed record ReferenceTypeOption(GuaranteeReferenceType Value, string Label);
    }

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
            _beneficiaryInput.Text = string.IsNullOrWhiteSpace(currentGuarantee.Beneficiary) ? currentGuarantee.Supplier : currentGuarantee.Beneficiary;
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
            fields.Children.Add(BuildField("المستفيد البديل", _beneficiaryInput));
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
                string.IsNullOrWhiteSpace(replacementBeneficiary) ? replacementSupplier : replacementBeneficiary,
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827"))
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

    public sealed class HistoryDialog : Window
    {
        private readonly GuaranteeRow _row;
        private readonly IReadOnlyList<Guarantee> _history;
        private readonly IReadOnlyList<WorkflowRequest> _requests;
        private readonly IGuaranteeHistoryDocumentService _historyDocuments;
        private readonly TabControl _tabs = new();
        private readonly ListBox _versionsList = new();
        private readonly ListBox _requestsList = new();
        private readonly TextBlock _detailTitle = WorkspaceSurfaceChrome.Text(17, FontWeights.Bold, "#0F172A");
        private readonly TextBlock _detailSubtitle = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#64748B");
        private readonly Border _detailStatusBorder = new();
        private readonly TextBlock _detailStatusText = new() { FontSize = 10, FontWeight = FontWeights.SemiBold };
        private readonly Image _detailBankLogo = new() { Width = 17, Height = 17 };
        private readonly TextBlock _detailBankText = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#64748B");
        private readonly TextBlock _detailHeadline = WorkspaceSurfaceChrome.Text(27, FontWeights.Bold, "#0F172A");
        private readonly TextBlock _detailCaption = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#94A3B8");
        private readonly TextBlock _detailLabel1 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLabel2 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLabel3 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLabel4 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLabel5 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLabel6 = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#94A3B8");
        private readonly TextBlock _detailLine1 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailLine2 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailLine3 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailLine4 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailLine5 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailLine6 = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
        private readonly TextBlock _detailNote = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#64748B");
        private readonly Button _openAttachmentsButton = new();
        private readonly Button _openLetterButton = new();
        private readonly Button _openResponseButton = new();
        private readonly Button _exportHistoryButton = new();
        private readonly Button _printHistoryButton = new();

        private HistoryDialog(GuaranteeRow row, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests)
        {
            _row = row;
            _historyDocuments = App.CurrentApp.GetRequiredService<IGuaranteeHistoryDocumentService>();
            _history = history
                .OrderByDescending(item => item.VersionNumber)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
            _requests = requests
                .OrderByDescending(item => item.RequestDate)
                .ThenByDescending(item => item.SequenceNumber)
                .ToList();

            Title = "سجل الضمان";
            UiInstrumentation.Identify(this, "Dialog.GuaranteeHistory", Title);
            UiInstrumentation.Identify(_tabs, "Dialog.GuaranteeHistory.Tabs", "تبويبات سجل الضمان");
            UiInstrumentation.Identify(_versionsList, "Dialog.GuaranteeHistory.VersionsList", "قائمة إصدارات الضمان");
            UiInstrumentation.Identify(_requestsList, "Dialog.GuaranteeHistory.RequestsList", "قائمة طلبات الضمان");
            Width = 920;
            Height = 620;
            MinWidth = 820;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushFrom("#F7F9FC");
            DialogWindowSupport.Attach(this, nameof(HistoryDialog));

            Content = BuildLayout();
            ReloadData();
        }

        public static void ShowFor(GuaranteeRow row, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests)
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                $"history:{row.RootId}",
                () => new HistoryDialog(row, history, requests),
                "سجل الضمان",
                "سجل هذا الضمان مفتوح بالفعل.");
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(BuildHeader());

            UIElement summary = BuildSummaryStrip();
            Grid.SetRow(summary, 2);
            root.Children.Add(summary);

            UIElement content = BuildMainContent();
            Grid.SetRow(content, 4);
            root.Children.Add(content);

            UIElement actions = BuildActions();
            Grid.SetRow(actions, 6);
            root.Children.Add(actions);

            return root;
        }

        private UIElement BuildHeader()
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = $"سجل الضمان - {_row.GuaranteeNo}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"{_row.Beneficiary} | {_row.Bank}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Children.Add(titleStack);

            var closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "White", "#D8E1EE", "#1F2937");
            UiInstrumentation.Identify(closeButton, "Dialog.GuaranteeHistory.CloseButton", "إغلاق سجل الضمان");
            closeButton.MinWidth = 92;
            closeButton.Click += (_, _) => Close();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);
            return header;
        }

        private UIElement BuildSummaryStrip()
        {
            Guarantee? currentVersion = _history.FirstOrDefault(item => item.IsCurrent) ?? _history.FirstOrDefault();
            string currentVersionLabel = currentVersion?.VersionLabel ?? "---";
            string lastUpdate = _history.FirstOrDefault()?.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---";

            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("الإصدارات", _history.Count.ToString("N0", CultureInfo.InvariantCulture), "#2563EB"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("الطلبات", _requests.Count.ToString("N0", CultureInfo.InvariantCulture), "#E09408"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("الإصدار الحالي", currentVersionLabel, "#16A34A"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("آخر تحديث", lastUpdate, "#0F172A"));
            return metrics;
        }

        private UIElement BuildMainContent()
        {
            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            _tabs.SelectionChanged += (_, _) => UpdateDetailPanel();
            var versionsTab = UiInstrumentation.Identify(
                new TabItem { Header = "الإصدارات", Content = BuildVersionsTab() },
                "Dialog.GuaranteeHistory.Tab.Versions",
                "تبويب الإصدارات");
            var requestsTab = UiInstrumentation.Identify(
                new TabItem { Header = "الطلبات", Content = BuildRequestsTab() },
                "Dialog.GuaranteeHistory.Tab.Requests",
                "تبويب الطلبات");
            _tabs.Items.Add(versionsTab);
            _tabs.Items.Add(requestsTab);
            grid.Children.Add(_tabs);

            UIElement detailPanel = UiInstrumentation.Identify(
                BuildDetailPanel(),
                "Dialog.GuaranteeHistory.DetailPanel",
                "لوحة تفاصيل سجل الضمان");
            Grid.SetColumn(detailPanel, 1);
            grid.Children.Add(detailPanel);
            return grid;
        }

        private UIElement BuildVersionsTab()
        {
            _versionsList.Style = WorkspaceSurfaceChrome.Style("ReferenceTableRowsListBox");
            _versionsList.SelectionChanged += (_, _) => UpdateDetailPanel();
            _versionsList.MouseDoubleClick += (_, _) => OpenSelectedAttachments();

            return BuildTabShell(
                BuildVersionsHeader(),
                _versionsList,
                _history.Count == 0 ? "لا توجد إصدارات محفوظة لهذا الضمان." : $"{_history.Count.ToString("N0", CultureInfo.InvariantCulture)} إصدار محفوظ");
        }

        private UIElement BuildRequestsTab()
        {
            _requestsList.Style = WorkspaceSurfaceChrome.Style("ReferenceTableRowsListBox");
            _requestsList.SelectionChanged += (_, _) => UpdateDetailPanel();
            _requestsList.MouseDoubleClick += (_, _) =>
            {
                if (SelectedRequest?.HasLetter == true)
                {
                    OpenSelectedLetter();
                }
            };

            return BuildTabShell(
                BuildRequestsHeader(),
                _requestsList,
                _requests.Count == 0 ? "لا توجد طلبات مرتبطة بهذه السلسلة." : $"{_requests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب مرتبط");
        }

        private static UIElement BuildTabShell(UIElement header, ListBox list, string summary)
        {
            var border = WorkspaceSurfaceChrome.Card(new Thickness(0));
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            Grid.SetRow(header, 0);
            grid.Children.Add(header);
            Grid.SetRow(list, 1);
            grid.Children.Add(list);

            var footer = new Border
            {
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E7EDF5"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Background = Brushes.White,
                Padding = new Thickness(12, 0, 12, 0)
            };
            footer.Child = new TextBlock
            {
                Text = summary,
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            border.Child = grid;
            return border;
        }

        private UIElement BuildVersionsHeader()
        {
            var header = new Grid
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderBand")
            };
            header.Children.Add(new Border
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderDivider")
            });

            var inner = CreateVersionsGrid();
            AddHeader(inner, "الإصدار", 0, true);
            AddHeader(inner, "الحالة", 1, false);
            AddHeader(inner, "الإنشاء", 2, false);
            AddHeader(inner, "الانتهاء", 3, false);
            AddHeader(inner, "القيمة", 4, false);
            AddHeader(inner, "المرفقات", 5, false);
            header.Children.Add(inner);
            return header;
        }

        private UIElement BuildRequestsHeader()
        {
            var header = new Grid
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderBand")
            };
            header.Children.Add(new Border
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderDivider")
            });

            var inner = CreateRequestsGrid();
            AddHeader(inner, "الطلب", 0, true);
            AddHeader(inner, "الحالة", 1, false);
            AddHeader(inner, "تاريخ الطلب", 2, false);
            AddHeader(inner, "القيمة المطلوبة", 3, false);
            AddHeader(inner, "الرد", 4, false);
            AddHeader(inner, "المستندات", 5, false);
            header.Children.Add(inner);
            return header;
        }

        private UIElement BuildDetailPanel()
        {
            _detailStatusBorder.Style = WorkspaceSurfaceChrome.Style("StatusPill");
            _detailStatusBorder.HorizontalAlignment = HorizontalAlignment.Left;
            _detailStatusBorder.Margin = new Thickness(0, 0, 0, 12);
            _detailStatusBorder.Child = _detailStatusText;

            var bankRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _detailBankLogo.Margin = new Thickness(7, 0, 0, 0);
            bankRow.Children.Add(_detailBankText);
            bankRow.Children.Add(_detailBankLogo);

            var content = new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    new TextBlock
                    {
                        Text = "تفاصيل السجل",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
                    },
                    _detailTitle,
                    _detailSubtitle,
                    _detailStatusBorder,
                    bankRow,
                    _detailHeadline,
                    _detailCaption,
                    WorkspaceSurfaceChrome.Divider(),
                    BuildInfoLine(_detailLabel1, _detailLine1),
                    BuildInfoLine(_detailLabel2, _detailLine2),
                    BuildInfoLine(_detailLabel3, _detailLine3),
                    BuildInfoLine(_detailLabel4, _detailLine4),
                    BuildInfoLine(_detailLabel5, _detailLine5),
                    BuildInfoLine(_detailLabel6, _detailLine6),
                    BuildInfoBlock("ملاحظات", _detailNote)
                }
            };

            return WorkspaceSurfaceChrome.BuildReferenceDetailPanel(content);
        }

        private UIElement BuildActions()
        {
            _openAttachmentsButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openAttachmentsButton.Content = "فتح مرفقات الإصدار";
            UiInstrumentation.Identify(_openAttachmentsButton, "Dialog.GuaranteeHistory.OpenAttachmentsButton", "فتح مرفقات الإصدار");
            _openAttachmentsButton.Click += (_, _) => OpenSelectedAttachments();

            _openLetterButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openLetterButton.Content = "فتح خطاب الطلب";
            UiInstrumentation.Identify(_openLetterButton, "Dialog.GuaranteeHistory.OpenLetterButton", "فتح خطاب الطلب");
            _openLetterButton.Click += (_, _) => OpenSelectedLetter();

            _openResponseButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openResponseButton.Content = "فتح رد البنك";
            UiInstrumentation.Identify(_openResponseButton, "Dialog.GuaranteeHistory.OpenResponseButton", "فتح رد البنك");
            _openResponseButton.Click += (_, _) => OpenSelectedResponse();

            _exportHistoryButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _exportHistoryButton.Content = "تصدير السجل";
            UiInstrumentation.Identify(_exportHistoryButton, "Dialog.GuaranteeHistory.ExportButton", "تصدير سجل الضمان");
            _exportHistoryButton.Click += (_, _) => ExportHistory();

            _printHistoryButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _printHistoryButton.Content = "طباعة السجل";
            UiInstrumentation.Identify(_printHistoryButton, "Dialog.GuaranteeHistory.PrintButton", "طباعة سجل الضمان");
            _printHistoryButton.Click += (_, _) => PrintHistory();

            var copyButton = WorkspaceSurfaceChrome.ActionButton("نسخ رقم الضمان", "White", "#D8E1EE", "#1F2937");
            UiInstrumentation.Identify(copyButton, "Dialog.GuaranteeHistory.CopyGuaranteeNoButton", "نسخ رقم الضمان");
            copyButton.MinWidth = 116;
            copyButton.Click += (_, _) => CopyGuaranteeNo();

            var closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "#2563EB", "#2563EB", "White");
            UiInstrumentation.Identify(closeButton, "Dialog.GuaranteeHistory.CloseFooterButton", "إغلاق سجل الضمان");
            closeButton.MinWidth = 94;
            closeButton.Click += (_, _) => Close();

            var actions = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 7,
                FlowDirection = FlowDirection.LeftToRight
            };

            foreach (Button button in new[]
                     {
                         _openAttachmentsButton,
                         _openLetterButton,
                         _openResponseButton,
                         _exportHistoryButton,
                         _printHistoryButton,
                         copyButton,
                         closeButton
                     })
            {
                button.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(button);
            }

            UpdateHistoryActionsAvailability();
            return actions;
        }

        private static Grid CreateVersionsGrid()
        {
            var grid = new Grid
            {
                Margin = new Thickness(9, 0, 9, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            return grid;
        }

        private static Grid CreateRequestsGrid()
        {
            var grid = new Grid
            {
                Margin = new Thickness(9, 0, 9, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            return grid;
        }

        private static void AddHeader(Grid grid, string text, int column, bool rightAligned)
        {
            var header = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style(rightAligned ? "TableHeaderRight" : "TableHeaderText")
            };
            Grid.SetColumn(header, column);
            grid.Children.Add(header);
        }

        private void CopyGuaranteeNo()
        {
            if (string.IsNullOrWhiteSpace(_row.GuaranteeNo))
            {
                MessageBox.Show("لا توجد قيمة متاحة لنسخ رقم الضمان.", "نسخ رقم الضمان", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(_row.GuaranteeNo);
                App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                    "تم نسخ رقم الضمان.",
                    "سجل الضمان • الحافظة جاهزة للاستخدام");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "نسخ رقم الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ReloadData()
        {
            _versionsList.Items.Clear();
            foreach (Guarantee item in _history)
            {
                _versionsList.Items.Add(BuildVersionRow(item));
            }

            _requestsList.Items.Clear();
            foreach (WorkflowRequest request in _requests)
            {
                _requestsList.Items.Add(BuildRequestRow(request));
            }

            if (_versionsList.Items.Count > 0)
            {
                _versionsList.SelectedIndex = 0;
            }

            if (_requestsList.Items.Count > 0)
            {
                _requestsList.SelectedIndex = 0;
            }

            UpdateDetailPanel();
            UpdateHistoryActionsAvailability();
        }

        private FrameworkElement BuildVersionRow(Guarantee item)
        {
            var row = CreateVersionsGrid();
            row.Tag = item;
            row.Height = 40;
            row.Children.Add(BuildCell(item.VersionLabel, 0, "TableCellRight"));
            row.Children.Add(BuildCell(item.IsCurrent ? "حالي" : "محفوظ", 1, "TableCellCenter", item.IsCurrent ? WorkspaceSurfaceChrome.BrushFrom("#16A34A") : WorkspaceSurfaceChrome.BrushFrom("#2563EB")));
            row.Children.Add(BuildCell(item.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), 2, "TableCellCenter"));
            row.Children.Add(BuildCell(item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), 3, "TableCellCenter"));
            row.Children.Add(BuildCell($"{item.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال", 4, "TableCellCenter"));
            row.Children.Add(BuildCell(item.AttachmentCount.ToString("N0", CultureInfo.InvariantCulture), 5, "TableCellCenter"));
            return row;
        }

        private FrameworkElement BuildRequestRow(WorkflowRequest request)
        {
            var row = CreateRequestsGrid();
            row.Tag = request;
            row.Height = 40;
            row.Children.Add(BuildCell(request.TypeLabel, 0, "TableCellRight"));
            row.Children.Add(BuildCell(request.StatusLabel, 1, "TableCellCenter", GetRequestStatusBrush(request.Status)));
            row.Children.Add(BuildCell(request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), 2, "TableCellCenter"));
            row.Children.Add(BuildCell(BuildRequestedValueSummary(request), 3, "TableCellCenter"));
            row.Children.Add(BuildCell(request.ResponseRecordedAt?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---", 4, "TableCellCenter"));
            row.Children.Add(BuildCell(BuildDocumentState(request), 5, "TableCellCenter"));
            return row;
        }

        private void UpdateDetailPanel()
        {
            if (_tabs.SelectedIndex == 1)
            {
                UpdateRequestDetails();
                return;
            }

            UpdateVersionDetails();
        }

        private void UpdateVersionDetails()
        {
            Guarantee? selected = SelectedVersion;
            if (selected == null)
            {
                ApplyDetailState(
                    "لا توجد إصدارات محفوظة",
                    "سيظهر هنا سجل السلسلة عند توفر إصدارات محفوظة.",
                    "لا يوجد سجل",
                    Tone.Info,
                    _row.Bank,
                    string.Empty,
                    "---",
                    "لا توجد قيمة",
                    ("الإصدار", "---"),
                    ("الحالة التشغيلية", "---"),
                    ("المرجع", "---"),
                    ("تاريخ الإنشاء", "---"),
                    ("تاريخ الانتهاء", "---"),
                    ("المرفقات", "---"),
                    "هذا الضمان لا يحتوي على سجل محفوظ حتى الآن.");
                _openAttachmentsButton.IsEnabled = false;
                _openLetterButton.IsEnabled = false;
                _openResponseButton.IsEnabled = false;
                return;
            }

            string beneficiary = string.IsNullOrWhiteSpace(selected.Beneficiary) ? selected.Supplier : selected.Beneficiary;
            Tone tone = selected.IsCurrent ? Tone.Success : Tone.Info;
            string reference = selected.HasReference
                ? $"{selected.ReferenceTypeLabel}: {selected.ReferenceNumber}"
                : "بدون مرجع";

            ApplyDetailState(
                $"الإصدار {selected.VersionLabel}",
                $"{selected.GuaranteeType} | {beneficiary}",
                selected.IsCurrent ? "الحالي" : "محفوظ",
                tone,
                selected.Bank,
                beneficiary,
                $"{selected.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                "القيمة المسجلة لهذا الإصدار",
                ("الإصدار", selected.VersionLabel),
                ("الحالة التشغيلية", selected.LifecycleStatusLabel),
                ("المرجع", reference),
                ("تاريخ الإنشاء", selected.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                ("تاريخ الانتهاء", selected.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                ("المرفقات", selected.AttachmentCount.ToString("N0", CultureInfo.InvariantCulture)),
                string.IsNullOrWhiteSpace(selected.Notes) ? "لا توجد ملاحظات محفوظة لهذا الإصدار." : selected.Notes);

            _openAttachmentsButton.IsEnabled = selected.AttachmentCount > 0;
            _openLetterButton.IsEnabled = false;
            _openResponseButton.IsEnabled = false;
        }

        private void UpdateRequestDetails()
        {
            WorkflowRequest? selected = SelectedRequest;
            if (selected == null)
            {
                ApplyDetailState(
                    "لا توجد طلبات مرتبطة",
                    "سيظهر هنا تسلسل الطلبات المرتبطة بهذه السلسلة.",
                    "بدون طلبات",
                    Tone.Info,
                    _row.Bank,
                    _row.Beneficiary,
                    "---",
                    "لا توجد قيمة مطلوبة",
                    ("نوع الطلب", "---"),
                    ("الحالة", "---"),
                    ("الطلب", "---"),
                    ("الرد", "---"),
                    ("الإصدار الأساس", "---"),
                    ("الإصدار الناتج", "---"),
                    "لا توجد طلبات مسجلة لهذه السلسلة.");
                _openAttachmentsButton.IsEnabled = false;
                _openLetterButton.IsEnabled = false;
                _openResponseButton.IsEnabled = false;
                return;
            }

            Tone tone = selected.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected => Tone.Danger,
                RequestStatus.Cancelled => Tone.Info,
                RequestStatus.Superseded => Tone.Info,
                _ => Tone.Info
            };

            string headline = selected.Type switch
            {
                RequestType.Reduction => $"{(selected.RequestedAmount ?? 0m).ToString("N0", CultureInfo.InvariantCulture)} ريال",
                RequestType.Extension => selected.RequestedExpiryDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---",
                RequestType.Replacement => string.IsNullOrWhiteSpace(selected.ReplacementGuaranteeNo) ? "استبدال" : selected.ReplacementGuaranteeNo,
                _ => selected.RequestedValueLabel
            };

            ApplyDetailState(
                selected.TypeLabel,
                $"تسلسل #{selected.SequenceNumber.ToString("N0", CultureInfo.InvariantCulture)} | {_row.Beneficiary}",
                selected.StatusLabel,
                tone,
                _row.Bank,
                _row.Beneficiary,
                headline,
                $"{GetRequestedFieldCaption(selected)}",
                ("نوع الطلب", selected.TypeLabel),
                ("الحالة", selected.StatusLabel),
                ("تاريخ الطلب", selected.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                ("تاريخ الرد", selected.ResponseRecordedAt?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---"),
                ("الإصدار الأساس", $"v{selected.BaseVersionId.ToString("N0", CultureInfo.InvariantCulture)}"),
                ("الإصدار الناتج", selected.ResultVersionId.HasValue ? $"v{selected.ResultVersionId.Value.ToString("N0", CultureInfo.InvariantCulture)}" : "---"),
                !string.IsNullOrWhiteSpace(selected.ResponseNotes)
                    ? selected.ResponseNotes
                    : !string.IsNullOrWhiteSpace(selected.Notes)
                        ? selected.Notes
                        : "لا توجد ملاحظات إضافية لهذا الطلب.");

            _openAttachmentsButton.IsEnabled = false;
            _openLetterButton.IsEnabled = selected.HasLetter;
            _openResponseButton.IsEnabled = selected.HasResponseDocument;
        }

        private void ApplyDetailState(
            string title,
            string subtitle,
            string status,
            Tone tone,
            string bank,
            string bankCaption,
            string headline,
            string caption,
            (string Label, string Value) line1,
            (string Label, string Value) line2,
            (string Label, string Value) line3,
            (string Label, string Value) line4,
            (string Label, string Value) line5,
            (string Label, string Value) line6,
            string note)
        {
            _detailTitle.Text = title;
            _detailSubtitle.Text = subtitle;
            _detailStatusText.Text = status;
            _detailStatusText.Foreground = TonePalette.Foreground(tone);
            _detailStatusBorder.Background = TonePalette.Background(tone);
            _detailStatusBorder.BorderBrush = TonePalette.Border(tone);
            _detailBankLogo.Source = GuaranteeRow.ResolveBankLogo(bank);
            _detailBankText.Text = string.IsNullOrWhiteSpace(bankCaption) ? bank : $"{bank} | {bankCaption}";
            _detailHeadline.Text = headline;
            _detailCaption.Text = caption;
            SetInfoLineText(_detailLabel1, _detailLine1, line1);
            SetInfoLineText(_detailLabel2, _detailLine2, line2);
            SetInfoLineText(_detailLabel3, _detailLine3, line3);
            SetInfoLineText(_detailLabel4, _detailLine4, line4);
            SetInfoLineText(_detailLabel5, _detailLine5, line5);
            SetInfoLineText(_detailLabel6, _detailLine6, line6);
            _detailNote.Text = note;
        }

        private static Grid BuildInfoLine(TextBlock labelBlock, TextBlock valueBlock)
        {
            labelBlock.VerticalAlignment = VerticalAlignment.Center;
            valueBlock.VerticalAlignment = VerticalAlignment.Center;

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(labelBlock);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
            return grid;
        }

        private static void SetInfoLineText(TextBlock labelBlock, TextBlock valueBlock, (string Label, string Value) line)
        {
            labelBlock.Text = line.Label;
            valueBlock.Text = line.Value;
        }

        private Border BuildInfoBlock(string title, TextBlock body)
        {
            var border = new Border
            {
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(body);
            border.Child = stack;
            return border;
        }

        private Guarantee? SelectedVersion => (_versionsList.SelectedItem as FrameworkElement)?.Tag as Guarantee;

        private WorkflowRequest? SelectedRequest => (_requestsList.SelectedItem as FrameworkElement)?.Tag as WorkflowRequest;

        private Guarantee ResolveSummaryGuarantee()
        {
            Guarantee? current = _history.FirstOrDefault(item => item.IsCurrent) ?? _history.FirstOrDefault();
            if (current != null)
            {
                return current;
            }

            return new Guarantee
            {
                Id = _row.Id,
                RootId = _row.RootId,
                GuaranteeNo = _row.GuaranteeNo,
                Beneficiary = _row.Beneficiary,
                Supplier = _row.Beneficiary,
                Bank = _row.Bank,
                Amount = _row.AmountValue,
                ExpiryDate = _row.ExpiryDateValue,
                CreatedAt = DateTime.Today
            };
        }

        private void UpdateHistoryActionsAvailability()
        {
            bool hasHistoryRecords = _history.Count > 0 || _requests.Count > 0;
            _exportHistoryButton.IsEnabled = hasHistoryRecords;
            _printHistoryButton.IsEnabled = hasHistoryRecords;
        }

        private void OpenSelectedAttachments()
        {
            if (SelectedVersion is { AttachmentCount: > 0 } version)
            {
                AttachmentPickerDialog.ShowFor(version.Attachments, $"history-attachments:{version.RootId}:{version.VersionNumber}");
            }
        }

        private void ExportHistory()
        {
            bool exported = _historyDocuments.ExportHistoryToExcel(ResolveSummaryGuarantee(), _history, _requests);
            if (!exported)
            {
                return;
            }

            App.CurrentApp.GetRequiredService<IUiDiagnosticsService>().RecordEvent(
                "guarantee.history",
                "export",
                new { _row.GuaranteeNo, _row.RootId, OutputPath = _historyDocuments.LastOutputPath ?? string.Empty });

            string savedFile = string.IsNullOrWhiteSpace(_historyDocuments.LastOutputPath)
                ? "تم حفظ الملف بنجاح"
                : Path.GetFileName(_historyDocuments.LastOutputPath);
            App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                $"تم تصدير سجل الضمان رقم {_row.GuaranteeNo}.",
                $"سجل الضمان • {savedFile}");
        }

        private void PrintHistory()
        {
            bool printed = _historyDocuments.PrintHistory(ResolveSummaryGuarantee(), _history, _requests);
            if (!printed)
            {
                return;
            }

            App.CurrentApp.GetRequiredService<IUiDiagnosticsService>().RecordEvent(
                "guarantee.history",
                "print",
                new { _row.GuaranteeNo, _row.RootId });

            App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                $"تم إرسال سجل الضمان رقم {_row.GuaranteeNo} إلى الطباعة.",
                "سجل الضمان • الطباعة");
        }

        private void OpenSelectedLetter()
        {
            if (SelectedRequest is { HasLetter: true } request)
            {
                OpenFile(request.LetterFilePath, "خطاب الطلب");
            }
        }

        private void OpenSelectedResponse()
        {
            if (SelectedRequest is { HasResponseDocument: true } request)
            {
                OpenFile(request.ResponseFilePath, "رد البنك");
            }
        }

        private static void OpenFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show($"تعذر فتح {label}. الملف غير موجود.", label, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static Brush GetRequestStatusBrush(RequestStatus status) => status switch
        {
            RequestStatus.Executed => WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
            RequestStatus.Pending => WorkspaceSurfaceChrome.BrushFrom("#E09408"),
            RequestStatus.Rejected => WorkspaceSurfaceChrome.BrushFrom("#EF4444"),
            RequestStatus.Cancelled => WorkspaceSurfaceChrome.BrushFrom("#2563EB"),
            RequestStatus.Superseded => WorkspaceSurfaceChrome.BrushFrom("#64748B"),
            _ => WorkspaceSurfaceChrome.BrushFrom("#64748B")
        };

        private static string BuildRequestedValueSummary(WorkflowRequest request) => request.Type switch
        {
            RequestType.Reduction => request.RequestedAmount.HasValue
                ? $"{request.RequestedAmount.Value.ToString("N0", CultureInfo.InvariantCulture)} ريال"
                : "---",
            RequestType.Extension => request.RequestedExpiryDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---",
            _ => request.RequestedValueLabel
        };

        private static string BuildDocumentState(WorkflowRequest request)
        {
            if (request.HasLetter && request.HasResponseDocument)
            {
                return "خطاب + رد";
            }

            if (request.HasLetter)
            {
                return "خطاب";
            }

            if (request.HasResponseDocument)
            {
                return "رد";
            }

            return "---";
        }

        private static string GetRequestedFieldCaption(WorkflowRequest request) => request.Type switch
        {
            RequestType.Reduction => "القيمة المطلوبة",
            RequestType.Extension => "تاريخ الانتهاء المطلوب",
            RequestType.Replacement => "رقم الضمان البديل",
            _ => "القيمة المطلوبة للطلب"
        };

        private static TextBlock BuildCell(string text, int column, string styleKey, Brush? foreground = null)
        {
            var cell = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style(styleKey)
            };

            if (foreground != null)
            {
                cell.Foreground = foreground;
            }

            Grid.SetColumn(cell, column);
            return cell;
        }
    }

    public sealed class OperationalInquiryDialog : Window
    {
        private readonly OperationalInquiryResult _result;
        private readonly IDatabaseService _database;
        private readonly IWorkflowService _workflow;
        private readonly IExcelService _excel;

        private OperationalInquiryDialog(
            OperationalInquiryResult result,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel)
        {
            _result = result;
            _database = database;
            _workflow = workflow;
            _excel = excel;

            Title = "الاستعلامات التشغيلية";
            UiInstrumentation.Identify(this, "Dialog.OperationalInquiry", Title);
            Width = 980;
            Height = 680;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushFrom("#F7F9FC");
            DialogWindowSupport.Attach(this, nameof(OperationalInquiryDialog));

            Content = BuildLayout();
        }

        public static void ShowFor(
            OperationalInquiryResult result,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel)
        {
            int inquiryRootId = result.CurrentGuarantee?.RootId
                ?? result.SelectedGuarantee?.RootId
                ?? result.ResultGuarantee?.RootId
                ?? 0;
            string windowKey = inquiryRootId > 0
                ? $"inquiry:{inquiryRootId}:{result.InquiryKey}"
                : $"inquiry:{result.InquiryKey}:{result.Subject}";

            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                windowKey,
                () => new OperationalInquiryDialog(result, database, workflow, excel),
                "الاستعلامات التشغيلية",
                "نتيجة هذا الاستعلام مفتوحة بالفعل.");
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(BuildHeader());

            UIElement summary = BuildSummaryStrip();
            Grid.SetRow(summary, 2);
            root.Children.Add(summary);

            UIElement content = BuildMainContent();
            Grid.SetRow(content, 4);
            root.Children.Add(content);

            UIElement actions = BuildActions();
            Grid.SetRow(actions, 6);
            root.Children.Add(actions);

            return root;
        }

        private UIElement BuildHeader()
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = _result.Title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = _result.Subject,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"آخر حدث مرجعي: {_result.EventDateLabel}",
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Children.Add(titleStack);

            var closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "White", "#D8E1EE", "#1F2937");
            UiInstrumentation.Identify(closeButton, "Dialog.OperationalInquiry.CloseButton", "إغلاق الاستعلام التشغيلي");
            closeButton.MinWidth = 92;
            closeButton.Click += (_, _) => Close();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);
            return header;
        }

        private UIElement BuildSummaryStrip()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            int evidenceCount = _result.Facts.Count(fact => !string.IsNullOrWhiteSpace(fact.Value) && fact.Value != "---");

            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("تاريخ الحدث", _result.EventDateLabel.Split(' ')[0], "#2563EB"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("خطاب الطلب", _result.CanOpenRequestLetter ? "موجود" : "غير متاح", "#E09408"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("رد البنك", _result.CanOpenResponseDocument ? "موجود" : "غير متاح", "#16A34A"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("الأدلة", evidenceCount.ToString("N0", CultureInfo.InvariantCulture), guarantee != null ? "#0F172A" : "#64748B"));
            return metrics;
        }

        private UIElement BuildMainContent()
        {
            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            UiInstrumentation.Identify(scroll, "Dialog.OperationalInquiry.ContentScrollViewer", "تمرير الاستعلام التشغيلي");

            var leftStack = new StackPanel();
            leftStack.Children.Add(BuildAnswerCard());
            leftStack.Children.Add(BuildFactsCard());
            leftStack.Children.Add(BuildTimelineCard());
            scroll.Content = leftStack;
            grid.Children.Add(scroll);

            UIElement detailPanel = UiInstrumentation.Identify(
                BuildDetailPanel(),
                "Dialog.OperationalInquiry.DetailPanel",
                "لوحة تفاصيل الاستعلام التشغيلي");
            Grid.SetColumn(detailPanel, 1);
            grid.Children.Add(detailPanel);
            return grid;
        }

        private UIElement BuildAnswerCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.AnswerCard", "بطاقة الجواب المختصر");
            card.Margin = new Thickness(0, 0, 0, 10);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "الجواب المختصر",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(new TextBlock
            {
                Text = _result.Answer,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = _result.Explanation,
                FontSize = 12,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            card.Child = stack;
            return card;
        }

        private UIElement BuildFactsCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.FactsCard", "بطاقة الأدلة");
            card.Margin = new Thickness(0, 0, 0, 10);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "ملخص الأدلة",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(WorkspaceSurfaceChrome.Divider());

            if (_result.Facts.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "لا توجد حقائق إضافية مرتبطة بهذا الاستعلام.",
                    FontSize = 11.5,
                    Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
                });
            }
            else
            {
                var wrap = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };

                foreach (OperationalInquiryFact fact in _result.Facts)
                {
                    wrap.Children.Add(BuildFactTile(fact));
                }

                stack.Children.Add(wrap);
            }

            card.Child = stack;
            return card;
        }

        private UIElement BuildTimelineCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.TimelineCard", "بطاقة التسلسل الزمني");

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "التسلسل الزمني الداعم",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(WorkspaceSurfaceChrome.Divider());

            if (!_result.HasTimeline)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "لا يوجد خط زمني إضافي لهذا الاستعلام.",
                    FontSize = 11.5,
                    Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
                });
            }
            else
            {
                foreach (OperationalInquiryTimelineEntry entry in _result.Timeline.OrderByDescending(item => item.Timestamp))
                {
                    stack.Children.Add(BuildTimelineRow(entry));
                }
            }

            card.Child = stack;
            return card;
        }

        private UIElement BuildDetailPanel()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            Tone tone = ResolveTone(guarantee);
            string statusText = BuildStatusText(guarantee);
            string bank = guarantee?.Bank ?? _result.CurrentGuarantee?.Bank ?? _result.SelectedGuarantee?.Bank ?? string.Empty;
            string beneficiary = guarantee == null
                ? _result.Subject
                : string.IsNullOrWhiteSpace(guarantee.Beneficiary) ? guarantee.Supplier : guarantee.Beneficiary;
            string headline = guarantee == null
                ? _result.EventDateLabel.Split(' ')[0]
                : $"{guarantee.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال";
            string caption = guarantee == null
                ? "تاريخ الحدث المرجعي"
                : "القيمة الحالية للسجل المرتبط";
            string relatedRequest = _result.RelatedRequest == null
                ? "---"
                : $"{_result.RelatedRequest.TypeLabel} #{_result.RelatedRequest.SequenceNumber.ToString("N0", CultureInfo.InvariantCulture)}";
            string resultVersion = _result.ResultGuarantee?.VersionLabel
                ?? _result.CurrentGuarantee?.VersionLabel
                ?? "---";
            TextBlock detailGuaranteeNo = WorkspaceSurfaceChrome.Text(17, FontWeights.Bold, "#0F172A");
            detailGuaranteeNo.Text = guarantee?.GuaranteeNo ?? _result.Title;
            detailGuaranteeNo.Margin = new Thickness(0, 8, 0, 0);

            TextBlock detailSubject = WorkspaceSurfaceChrome.Text(11, FontWeights.SemiBold, "#64748B");
            detailSubject.Text = _result.Subject;
            detailSubject.Margin = new Thickness(0, 4, 0, 0);

            TextBlock detailHeadline = WorkspaceSurfaceChrome.Text(27, FontWeights.Bold, "#0F172A");
            detailHeadline.Text = headline;
            detailHeadline.Margin = new Thickness(0, 10, 0, 0);

            TextBlock detailCaption = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#94A3B8");
            detailCaption.Text = caption;
            detailCaption.Margin = new Thickness(0, 3, 0, 0);

            var statusTextBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = TonePalette.Foreground(tone)
            };

            var statusBorder = new Border
            {
                Style = WorkspaceSurfaceChrome.Style("StatusPill"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12),
                Background = TonePalette.Background(tone),
                BorderBrush = TonePalette.Border(tone),
                Child = statusTextBlock
            };

            var bankRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            bankRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(beneficiary) ? bank : $"{bank} | {beneficiary}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Center
            });
            bankRow.Children.Add(new Image
            {
                Source = GuaranteeRow.ResolveBankLogo(bank),
                Width = 17,
                Height = 17,
                Margin = new Thickness(7, 0, 0, 0)
            });

            var content = new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    new TextBlock
                    {
                        Text = "سياق النتيجة",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
                    },
                    detailGuaranteeNo,
                    detailSubject,
                    statusBorder,
                    bankRow,
                    detailHeadline,
                    detailCaption,
                    WorkspaceSurfaceChrome.Divider(),
                    BuildInfoLine("الاستعلام", _result.Title),
                    BuildInfoLine("الموضوع", _result.Subject),
                    BuildInfoLine("رقم الضمان", guarantee?.GuaranteeNo ?? "---"),
                    BuildInfoLine("الطلب المرتبط", relatedRequest),
                    BuildInfoLine("الإصدار الناتج", resultVersion),
                    BuildInfoLine("آخر حدث", _result.EventDateLabel),
                    BuildInfoBlock("تفسير إضافي", _result.Explanation)
                }
            };

            return WorkspaceSurfaceChrome.BuildReferenceDetailPanel(content);
        }

        private UIElement BuildActions()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            bool hasAttachments = guarantee?.Attachments?.Count > 0;

            Button openHistoryButton = WorkspaceSurfaceChrome.ActionButton("فتح السجل", "White", "#D8E1EE", "#1F2937");
            openHistoryButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openHistoryButton, "Dialog.OperationalInquiry.OpenHistoryButton", "فتح سجل الضمان");
            openHistoryButton.IsEnabled = _result.CanOpenHistory;
            openHistoryButton.ToolTip = _result.CanOpenHistory
                ? "يفتح سجل الضمان المرتبط بهذا الجواب."
                : "لا يوجد سجل ضمان مرتبط بهذا الجواب التشغيلي.";
            ToolTipService.SetShowOnDisabled(openHistoryButton, true);
            openHistoryButton.Click += (_, _) => OpenHistory();

            Button attachmentsButton = WorkspaceSurfaceChrome.ActionButton("مرفقات الإصدار", "White", "#D8E1EE", "#1F2937");
            attachmentsButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(attachmentsButton, "Dialog.OperationalInquiry.OpenAttachmentsButton", "فتح مرفقات الإصدار");
            attachmentsButton.IsEnabled = hasAttachments;
            attachmentsButton.ToolTip = hasAttachments
                ? "يفتح مرفقات الإصدار الحالي للضمان."
                : "لا توجد مرفقات مرتبطة بالإصدار الحالي لهذا الضمان.";
            ToolTipService.SetShowOnDisabled(attachmentsButton, true);
            attachmentsButton.Click += (_, _) => OpenAttachments();

            Button openLetterButton = WorkspaceSurfaceChrome.ActionButton("خطاب الطلب", "White", "#D8E1EE", "#1F2937");
            openLetterButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openLetterButton, "Dialog.OperationalInquiry.OpenLetterButton", "فتح خطاب الطلب");
            openLetterButton.IsEnabled = _result.CanOpenRequestLetter;
            openLetterButton.ToolTip = _result.CanOpenRequestLetter
                ? "يفتح خطاب الطلب المرتبط بهذا الجواب."
                : "لا يوجد خطاب طلب مرتبط بالسجل أو الطلب الذي استند إليه هذا الجواب.";
            ToolTipService.SetShowOnDisabled(openLetterButton, true);
            openLetterButton.Click += (_, _) => OpenLetter();

            Button openResponseButton = WorkspaceSurfaceChrome.ActionButton("رد البنك", "White", "#D8E1EE", "#1F2937");
            openResponseButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openResponseButton, "Dialog.OperationalInquiry.OpenResponseButton", "فتح رد البنك");
            openResponseButton.IsEnabled = _result.CanOpenResponseDocument;
            openResponseButton.ToolTip = _result.CanOpenResponseDocument
                ? "يفتح مستند رد البنك المرتبط بهذا الجواب."
                : "لا يوجد مستند رد بنك مرتبط بالسجل أو الطلب الذي استند إليه هذا الجواب.";
            ToolTipService.SetShowOnDisabled(openResponseButton, true);
            openResponseButton.Click += (_, _) => OpenResponse();

            Button exportButton = WorkspaceSurfaceChrome.ActionButton("تقرير الضمان", "White", "#D8E1EE", "#1F2937");
            exportButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(exportButton, "Dialog.OperationalInquiry.ExportReportButton", "تصدير تقرير الضمان");
            exportButton.IsEnabled = guarantee != null;
            exportButton.ToolTip = guarantee != null
                ? "يصدر تقرير الضمان الحالي."
                : "لا يوجد ضمان محدد حاليًا لتصدير تقريره.";
            ToolTipService.SetShowOnDisabled(exportButton, true);
            exportButton.Click += (_, _) => ExportGuaranteeReport();

            Button closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "#2563EB", "#2563EB", "White");
            closeButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            UiInstrumentation.Identify(closeButton, "Dialog.OperationalInquiry.CloseFooterButton", "إغلاق الاستعلام التشغيلي");
            closeButton.Click += (_, _) => Close();

            var actions = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 6,
                FlowDirection = FlowDirection.LeftToRight
            };

            foreach (Button button in new[]
                     {
                         openHistoryButton,
                         attachmentsButton,
                         openLetterButton,
                         openResponseButton,
                         exportButton,
                         closeButton
                     })
            {
                button.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(button);
            }

            return actions;
        }

        private Border BuildFactTile(OperationalInquiryFact fact)
        {
            var border = new Border
            {
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Width = 210
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = fact.Label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                TextAlignment = TextAlignment.Right
            });
            stack.Children.Add(new TextBlock
            {
                Text = fact.Value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Right
            });
            border.Child = stack;
            return border;
        }

        private Border BuildTimelineRow(OperationalInquiryTimelineEntry entry)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var date = new TextBlock
            {
                Text = entry.TimestampLabel,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Top,
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.Children.Add(date);

            var stack = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };
            stack.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = 12.2,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = entry.Details,
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            border.Child = grid;
            return border;
        }

        private static Grid BuildInfoLine(string label, string value)
        {
            TextBlock valueBlock = WorkspaceSurfaceChrome.Text(12, FontWeights.SemiBold, "#0F172A");
            valueBlock.Text = value;
            return WorkspaceSurfaceChrome.InfoLine(label, valueBlock);
        }

        private static Border BuildInfoBlock(string title, string value)
        {
            var body = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#64748B");
            body.Text = value;

            var border = new Border
            {
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(body);
            border.Child = stack;
            return border;
        }

        private Guarantee? ResolveFocusGuarantee()
        {
            int? guaranteeId = _result.ResultGuarantee?.Id
                ?? _result.CurrentGuarantee?.Id
                ?? _result.SelectedGuarantee?.Id;

            if (!guaranteeId.HasValue)
            {
                return null;
            }

            return _database.GetGuaranteeById(guaranteeId.Value)
                ?? _result.ResultGuarantee
                ?? _result.CurrentGuarantee
                ?? _result.SelectedGuarantee;
        }

        private Tone ResolveTone(Guarantee? guarantee)
        {
            if (_result.RelatedRequest != null)
            {
                return _result.RelatedRequest.Status switch
                {
                    RequestStatus.Executed => Tone.Success,
                    RequestStatus.Pending => Tone.Warning,
                    RequestStatus.Rejected => Tone.Danger,
                    RequestStatus.Cancelled => Tone.Info,
                    RequestStatus.Superseded => Tone.Info,
                    _ => Tone.Info
                };
            }

            if (guarantee == null)
            {
                return Tone.Info;
            }

            if (guarantee.IsExpired && guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active)
            {
                return Tone.Danger;
            }

            if (guarantee.IsExpiringSoon)
            {
                return Tone.Warning;
            }

            return guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active
                ? Tone.Success
                : Tone.Info;
        }

        private string BuildStatusText(Guarantee? guarantee)
        {
            if (_result.RelatedRequest != null)
            {
                return _result.RelatedRequest.StatusLabel;
            }

            if (guarantee != null)
            {
                return guarantee.LifecycleStatusLabel;
            }

            return "جواب تشغيلي";
        }

        private void OpenHistory()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee == null)
            {
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            Guarantee currentGuarantee = _database.GetCurrentGuaranteeByRootId(rootId) ?? guarantee;
            List<Guarantee> history = _database.GetGuaranteeHistory(currentGuarantee.Id);
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            GuaranteeRow row = GuaranteeRow.FromGuarantee(currentGuarantee, requests);

            HistoryDialog.ShowFor(row, history, requests);
        }

        private void OpenAttachments()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee?.Attachments?.Count > 0)
            {
                AttachmentPickerDialog.ShowFor(guarantee.Attachments);
            }
        }

        private void OpenLetter()
        {
            if (_result.RelatedRequest is not { HasLetter: true } request)
            {
                return;
            }

            _workflow.OpenRequestLetter(request);
        }

        private void OpenResponse()
        {
            if (_result.RelatedRequest is not { HasResponseDocument: true } request)
            {
                return;
            }

            _workflow.OpenResponseDocument(request);
        }

        private void ExportGuaranteeReport()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee == null)
            {
                return;
            }

            bool exported = _excel.ExportSingleGuaranteeReport(guarantee);
            if (!exported)
            {
                return;
            }

            string savedFile = string.IsNullOrWhiteSpace(_excel.LastOutputPath)
                ? "تم حفظ الملف بنجاح"
                : Path.GetFileName(_excel.LastOutputPath);
            App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                $"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}.",
                $"الاستعلامات التشغيلية • {savedFile}");
        }
    }

    public sealed class AttachmentPickerDialog : Window
    {
        private readonly ListBox _list = new();

        private AttachmentPickerDialog(IReadOnlyList<AttachmentRecord> attachments)
        {
            Title = "المرفقات";
            UiInstrumentation.Identify(this, "Dialog.AttachmentPicker", Title);
            UiInstrumentation.Identify(_list, "Dialog.AttachmentPicker.List", "قائمة المرفقات");
            Width = 460;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(AttachmentPickerDialog));

            var root = new DockPanel { Margin = new Thickness(16) };
            foreach (AttachmentRecord attachment in attachments)
            {
                _list.Items.Add(AttachmentItem.FromAttachment(attachment));
            }

            _list.DisplayMemberPath = nameof(AttachmentItem.Name);
            _list.MouseDoubleClick += (_, _) => OpenSelected();

            var openButton = UiInstrumentation.Identify(
                new Button { Content = "فتح", Width = 90, Height = 32, Margin = new Thickness(0, 12, 0, 0) },
                "Dialog.AttachmentPicker.OpenButton",
                "فتح");
            openButton.Click += (_, _) => OpenSelected();
            DockPanel.SetDock(openButton, Dock.Bottom);
            root.Children.Add(openButton);
            root.Children.Add(_list);
            Content = root;
        }

        public static void ShowFor(IReadOnlyList<AttachmentRecord> attachments, string windowKey = "attachments")
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                windowKey,
                () => new AttachmentPickerDialog(attachments),
                "المرفقات",
                "نافذة المرفقات مفتوحة بالفعل.");
        }

        private void OpenSelected()
        {
            if (_list.SelectedItem is AttachmentItem item && File.Exists(item.FilePath))
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }
    }

    public sealed class RequestsDialog : Window
    {
        private readonly Func<IReadOnlyList<WorkflowRequest>> _loadRequests;
        private readonly IWorkflowService _workflow;
        private readonly Action _onChanged;
        private readonly ListBox _list = new();
        private readonly Button _registerButton = new();
        private readonly Button _letterButton = new();
        private readonly Button _responseButton = new();
        private readonly Button _closeButton = new();
        private readonly TextBlock _summary = new()
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
            Margin = new Thickness(0, 0, 0, 10)
        };

        private RequestsDialog(Func<IReadOnlyList<WorkflowRequest>> loadRequests, IWorkflowService workflow, Action? onChanged, int? initialRequestId)
        {
            _loadRequests = loadRequests;
            _workflow = workflow;
            _onChanged = onChanged ?? (() => { });
            Title = "طلبات الضمان";
            UiInstrumentation.Identify(this, "Dialog.GuaranteeRequests", Title);
            UiInstrumentation.Identify(_list, "Dialog.GuaranteeRequests.List", "قائمة الطلبات");
            Width = 520;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(RequestsDialog));
            PreviewKeyDown += (_, eventArgs) =>
            {
                if (eventArgs.Key == Key.Escape)
                {
                    Close();
                    eventArgs.Handled = true;
                }
            };

            var root = new DockPanel { Margin = new Thickness(16) };
            _list.DisplayMemberPath = nameof(RequestDisplayItem.Display);
            _list.SelectionChanged += (_, _) => UpdateActionAvailability();
            DockPanel.SetDock(_summary, Dock.Top);
            root.Children.Add(_summary);
            root.Children.Add(BuildActions());
            root.Children.Add(_list);
            Content = root;
            ReloadRequests(initialRequestId);
        }

        public static void ShowFor(
            Func<IReadOnlyList<WorkflowRequest>> loadRequests,
            IWorkflowService workflow,
            Action? onChanged = null,
            string windowKey = "guarantee-requests",
            int? initialRequestId = null)
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                windowKey,
                () => new RequestsDialog(loadRequests, workflow, onChanged, initialRequestId),
                "طلبات الضمان",
                "طلبات هذا الضمان مفتوحة بالفعل.");
        }

        private StackPanel BuildActions()
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            _registerButton.Content = "تسجيل رد";
            _registerButton.Width = 94;
            _registerButton.Height = 32;
            _registerButton.Margin = new Thickness(8, 0, 0, 0);
            _registerButton.Click += (_, _) => RegisterSelectedResponse();
            UiInstrumentation.Identify(_registerButton, "Dialog.GuaranteeRequests.RegisterResponseButton", "تسجيل رد");
            ToolTipService.SetShowOnDisabled(_registerButton, true);

            _letterButton.Content = "فتح الخطاب";
            _letterButton.Width = 94;
            _letterButton.Height = 32;
            _letterButton.Margin = new Thickness(8, 0, 0, 0);
            _letterButton.Click += (_, _) => OpenLetter();
            UiInstrumentation.Identify(_letterButton, "Dialog.GuaranteeRequests.OpenLetterButton", "فتح الخطاب");
            ToolTipService.SetShowOnDisabled(_letterButton, true);

            _responseButton.Content = "فتح الرد";
            _responseButton.Width = 94;
            _responseButton.Height = 32;
            _responseButton.Click += (_, _) => OpenResponse();
            UiInstrumentation.Identify(_responseButton, "Dialog.GuaranteeRequests.OpenResponseButton", "فتح الرد");
            ToolTipService.SetShowOnDisabled(_responseButton, true);

            _closeButton.Content = "إغلاق";
            _closeButton.Width = 94;
            _closeButton.Height = 32;
            _closeButton.Margin = new Thickness(8, 0, 0, 0);
            _closeButton.IsCancel = true;
            _closeButton.Click += (_, _) => Close();
            UiInstrumentation.Identify(_closeButton, "Dialog.GuaranteeRequests.CloseButton", "إغلاق طلبات الضمان");

            actions.Children.Add(_registerButton);
            actions.Children.Add(_letterButton);
            actions.Children.Add(_responseButton);
            actions.Children.Add(_closeButton);
            return actions;
        }

        private WorkflowRequest? SelectedRequest => (_list.SelectedItem as RequestDisplayItem)?.Request;

        private void ReloadRequests(int? requestIdToSelect = null)
        {
            _list.Items.Clear();
            IReadOnlyList<WorkflowRequest> requests = _loadRequests()
                .OrderByDescending(item => item.RequestDate)
                .ThenByDescending(item => item.SequenceNumber)
                .ToList();

            foreach (WorkflowRequest request in requests)
            {
                var item = new RequestDisplayItem(request);
                _list.Items.Add(item);
                if (requestIdToSelect.HasValue && request.Id == requestIdToSelect.Value)
                {
                    _list.SelectedItem = item;
                }
            }

            if (_list.SelectedItem == null && _list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            _summary.Text = requests.Count == 0
                ? "لا توجد طلبات مرتبطة بهذا الضمان."
                : $"{requests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب مرتبط بهذا الضمان";

            UpdateActionAvailability();
        }

        private void RegisterSelectedResponse()
        {
            if (SelectedRequest is not { Status: RequestStatus.Pending } request)
            {
                MessageBox.Show("اختر طلباً معلقاً أولاً.", "طلبات الضمان", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BankResponseDialog.TryShow([request], out int requestId, out RequestStatus status, out string notes, out string responsePath))
            {
                return;
            }

            try
            {
                _workflow.RecordBankResponse(requestId, status, notes, string.IsNullOrWhiteSpace(responsePath) ? null : responsePath);
                _onChanged();
                ReloadRequests(requestId);
                App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                    "تم تسجيل رد البنك وتحديث طلبات الضمان.",
                    "طلبات الضمان • داخل ملف الضمان");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "طلبات الضمان", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenLetter()
        {
            if (SelectedRequest is { HasLetter: true } request)
            {
                _workflow.OpenRequestLetter(request);
            }
        }

        private void OpenResponse()
        {
            if (SelectedRequest is { HasResponseDocument: true } request)
            {
                _workflow.OpenResponseDocument(request);
            }
        }

        private void UpdateActionAvailability()
        {
            WorkflowRequest? request = SelectedRequest;
            bool hasSelection = request != null;
            bool canRegisterResponse = request?.Status == RequestStatus.Pending;
            bool canOpenLetter = request?.HasLetter == true;
            bool canOpenResponse = request?.HasResponseDocument == true;

            _registerButton.IsEnabled = canRegisterResponse;
            _registerButton.ToolTip = !hasSelection
                ? "اختر طلباً أولاً."
                : canRegisterResponse
                    ? "يسجل رد البنك للطلب المحدد."
                    : "هذا الطلب ليس معلقاً، لذلك لا يحتاج إلى تسجيل رد جديد.";

            _letterButton.IsEnabled = canOpenLetter;
            _letterButton.ToolTip = !hasSelection
                ? "اختر طلباً أولاً."
                : canOpenLetter
                    ? "يفتح خطاب الطلب المرتبط بهذا السجل."
                    : "لا يوجد خطاب محفوظ لهذا الطلب.";

            _responseButton.IsEnabled = canOpenResponse;
            _responseButton.ToolTip = !hasSelection
                ? "اختر طلباً أولاً."
                : canOpenResponse
                    ? "يفتح مستند رد البنك المسجل لهذا الطلب."
                    : "لا يوجد مستند رد بنك محفوظ لهذا الطلب.";
        }
    }
}
