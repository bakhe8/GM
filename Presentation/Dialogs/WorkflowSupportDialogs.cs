using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using Microsoft.Win32;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class GuidedTextPromptDialog : Window
    {
        private readonly TextBox _input;
        private string _result = string.Empty;

        private GuidedTextPromptDialog(
            string title,
            string prompt,
            string label,
            string confirmText,
            string initialValue,
            string? hint)
        {
            Title = title;
            Width = 430;
            Height = string.IsNullOrWhiteSpace(hint) ? 250 : 310;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(GuidedTextPromptDialog), Accept, "أكمل النافذة الحالية أو أغلقها أولاً.");

            var root = new DockPanel { Margin = new Thickness(16) };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var confirmButton = new Button
            {
                Content = confirmText,
                Width = 122,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            confirmButton.Click += (_, _) => Accept();

            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 90,
                Height = 32,
                IsCancel = true
            };

            actions.Children.Add(confirmButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = prompt,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                Margin = new Thickness(0, 0, 0, 6)
            });

            _input = new TextBox
            {
                Text = initialValue,
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
            content.Children.Add(_input);

            if (!string.IsNullOrWhiteSpace(hint))
            {
                content.Children.Add(new Border
                {
                    Margin = new Thickness(0, 12, 0, 0),
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9EC")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6DE99")),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = hint,
                        FontSize = 11,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E09408")),
                        TextWrapping = TextWrapping.Wrap
                    }
                });
            }

            root.Children.Add(content);
            Content = root;

            Loaded += (_, _) =>
            {
                _input.Focus();
                _input.SelectAll();
            };
        }

        public static bool TryShow(
            string title,
            string prompt,
            string label,
            string confirmText,
            string initialValue,
            out string value,
            string? hint = null)
        {
            var dialog = new GuidedTextPromptDialog(title, prompt, label, confirmText, initialValue, hint)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true;
            value = accepted ? dialog._result : string.Empty;
            return accepted;
        }

        private void Accept()
        {
            string value = _input.Text.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show("يرجى إدخال القيمة المطلوبة قبل المتابعة.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _result = value;
            DialogResult = true;
        }
    }

    public sealed class EligibleGuaranteePickerDialog : Window
    {
        private readonly ListBox _list = new();
        private Guarantee? _selectedGuarantee;

        private EligibleGuaranteePickerDialog(string title, string description, IReadOnlyList<Guarantee> guarantees)
        {
            Title = title;
            Width = 700;
            Height = 520;
            MinWidth = 620;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(EligibleGuaranteePickerDialog), Accept, "أكمل اختيار الضمان المؤهل أو أغلق النافذة أولاً.");

            var root = new DockPanel { Margin = new Thickness(16) };

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(header, Dock.Top);
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            });
            header.Children.Add(new TextBlock
            {
                Text = description,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                TextWrapping = TextWrapping.Wrap
            });
            root.Children.Add(header);

            _list.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE"));
            _list.BorderThickness = new Thickness(1);
            _list.Background = Brushes.White;
            _list.Padding = new Thickness(6);
            foreach (Guarantee guarantee in guarantees.OrderBy(item => item.GuaranteeNo))
            {
                _list.Items.Add(new EligibleGuaranteeOption(guarantee));
            }

            _list.DisplayMemberPath = nameof(EligibleGuaranteeOption.Display);
            _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
            root.Children.Add(_list);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var confirmButton = new Button
            {
                Content = "متابعة",
                Width = 104,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            confirmButton.Click += (_, _) => Accept();
            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 90,
                Height = 32,
                IsCancel = true
            };
            actions.Children.Add(confirmButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);

            Content = root;
        }

        public static bool TryShow(string title, string description, IReadOnlyList<Guarantee> guarantees, out Guarantee? selectedGuarantee)
        {
            var dialog = new EligibleGuaranteePickerDialog(title, description, guarantees)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true && dialog._selectedGuarantee != null;
            selectedGuarantee = dialog._selectedGuarantee;
            return accepted;
        }

        private void Accept()
        {
            if (_list.SelectedItem is not EligibleGuaranteeOption option)
            {
                MessageBox.Show("اختر ضمانًا مؤهلًا أولًا.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _selectedGuarantee = option.Guarantee;
            DialogResult = true;
        }

        private sealed record EligibleGuaranteeOption(Guarantee Guarantee)
        {
            public string Display =>
                $"{Guarantee.GuaranteeNo} | {Guarantee.Supplier} | {Guarantee.Bank} | {Guarantee.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال | {Guarantee.ExpiryDate:yyyy/MM/dd}";
        }
    }

    public sealed class AttachResponseDocumentDialog : Window
    {
        private readonly TextBox _notesInput = new();
        private readonly TextBlock _pathLabel = new();
        private string _filePath = string.Empty;
        private string _notes = string.Empty;

        private AttachResponseDocumentDialog(WorkflowRequestListItem item)
        {
            Title = "إلحاق مستند رد البنك";
            Width = 480;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(AttachResponseDocumentDialog), Accept, "أكمل إلحاق مستند الرد أو أغلق النافذة الحالية أولاً.");
            UiInstrumentation.Identify(this, "Dialog.AttachResponseDocument", Title);

            var root = new DockPanel { Margin = new Thickness(16) };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 14, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);
            var saveButton = new Button
            {
                Content = "إلحاق المستند",
                Width = 112,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            saveButton.Click += (_, _) => Accept();
            UiInstrumentation.Identify(saveButton, "Dialog.AttachResponseDocument.SaveButton", "إلحاق المستند");
            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 90,
                Height = 32,
                IsCancel = true
            };
            UiInstrumentation.Identify(cancelButton, "Dialog.AttachResponseDocument.CancelButton", "إلغاء");
            actions.Children.Add(saveButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = $"{item.GuaranteeNo} | {item.Request.TypeLabel} | {item.Request.StatusLabel}",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            });
            content.Children.Add(new TextBlock
            {
                Text = "أرفق مستند رد البنك لهذا الطلب المغلق حتى يبقى ملفه مكتملًا داخل النظام.",
                Margin = new Thickness(0, 6, 0, 12),
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                TextWrapping = TextWrapping.Wrap
            });

            var fileRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var browseButton = new Button
            {
                Content = "اختيار المستند",
                Width = 112,
                Height = 30
            };
            browseButton.Click += (_, _) => BrowseFile();
            UiInstrumentation.Identify(browseButton, "Dialog.AttachResponseDocument.ChooseFileButton", "اختيار المستند");
            fileRow.Children.Add(browseButton);

            _pathLabel.Text = "لم يتم اختيار ملف بعد";
            _pathLabel.FontSize = 11;
            _pathLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            _pathLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            _pathLabel.Margin = new Thickness(10, 7, 0, 0);
            UiInstrumentation.Identify(_pathLabel, "Dialog.AttachResponseDocument.FileSummary", "ملخص المستند");
            Grid.SetColumn(_pathLabel, 1);
            fileRow.Children.Add(_pathLabel);
            content.Children.Add(fileRow);

            content.Children.Add(new TextBlock
            {
                Text = "ملاحظات إضافية",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                Margin = new Thickness(0, 0, 0, 6)
            });

            _notesInput.Text = "تم إلحاق مستند رد البنك بعد إغلاق الطلب.";
            _notesInput.Height = 92;
            _notesInput.AcceptsReturn = true;
            _notesInput.TextWrapping = TextWrapping.Wrap;
            _notesInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _notesInput.Padding = new Thickness(8);
            _notesInput.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE"));
            _notesInput.BorderThickness = new Thickness(1);
            UiInstrumentation.Identify(_notesInput, "Dialog.AttachResponseDocument.NotesInput", "ملاحظات إضافية");
            content.Children.Add(_notesInput);

            root.Children.Add(content);
            Content = root;
        }

        public static bool TryShow(WorkflowRequestListItem item, out string filePath, out string notes)
        {
            var dialog = new AttachResponseDocumentDialog(item)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true;
            filePath = accepted ? dialog._filePath : string.Empty;
            notes = accepted ? dialog._notes : string.Empty;
            return accepted;
        }

        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Document Files|*.pdf;*.jpg;*.jpeg;*.png;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _filePath = dialog.FileName;
            _pathLabel.Text = dialog.FileName;
            _pathLabel.ToolTip = dialog.FileName;
        }

        private void Accept()
        {
            if (string.IsNullOrWhiteSpace(_filePath))
            {
                MessageBox.Show("يرجى اختيار مستند رد البنك قبل المتابعة.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _notes = _notesInput.Text.Trim();
            DialogResult = true;
        }
    }
}
