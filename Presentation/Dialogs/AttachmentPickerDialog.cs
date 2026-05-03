using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class AttachmentPickerDialog : Window
    {
        private readonly ListBox _list = new();

        private AttachmentPickerDialog(IReadOnlyList<AttachmentRecord> attachments, string title)
        {
            Title = title;
            UiInstrumentation.Identify(this, "Dialog.AttachmentPicker", Title);
            UiInstrumentation.Identify(_list, "Dialog.AttachmentPicker.List", "قائمة المرفقات");
            Width = 460;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(AttachmentPickerDialog));

            var root = new DockPanel { Margin = new Thickness(16) };
            foreach (AttachmentRecord attachment in attachments)
            {
                AttachmentItem item = AttachmentItem.FromAttachment(attachment);
                _list.Items.Add(BuildAttachmentRow(item));
            }

            _list.MouseDoubleClick += (_, _) => OpenSelected();

            var openButton = UiInstrumentation.Identify(
                new Button
                {
                    Content = "فتح"
                },
                "Dialog.AttachmentPicker.OpenButton",
                "فتح");
            openButton.Click += (_, _) => OpenSelected();
            var actions = DialogFormSupport.BuildSingleActionBar(openButton, 96);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            root.Children.Add(_list);
            Content = root;
        }

        public static void ShowFor(IReadOnlyList<AttachmentRecord> attachments, string windowKey = "attachments", string? title = null)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(title) ? "المرفقات" : title;
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                windowKey,
                () => new AttachmentPickerDialog(attachments, resolvedTitle),
                resolvedTitle,
                "نافذة المرفقات مفتوحة بالفعل.");
        }

        private void OpenSelected()
        {
            if (_list.SelectedItem is ListBoxItem { Tag: AttachmentItem item } && File.Exists(item.FilePath))
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }

        private static ListBoxItem BuildAttachmentRow(AttachmentItem item)
        {
            string[] metadataParts = string.Equals(item.Size, "---", System.StringComparison.Ordinal)
                ? new[] { item.FileKind, item.Date }
                : new[] { item.FileKind, item.Size, item.Date };

            var type = new TextBlock
            {
                Text = item.DocumentType,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Primary"),
                TextAlignment = TextAlignment.Right
            };
            var name = new TextBlock
            {
                Text = item.Name,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Primary"),
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var meta = new TextBlock
            {
                Text = string.Join(" • ", metadataParts),
                FontSize = 10,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Secondary"),
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(type);
            stack.Children.Add(name);
            stack.Children.Add(meta);

            var row = new ListBoxItem
            {
                Tag = item,
                Content = stack,
                Style = WorkspaceSurfaceChrome.Style("DialogListBoxItem")
            };
            AutomationProperties.SetName(row, item.Display);
            return row;
        }
    }
}
