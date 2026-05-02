using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
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
            FontFamily = UiTypography.DefaultFontFamily;
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
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
}
