using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
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
                _list.Items.Add(AttachmentItem.FromAttachment(attachment));
            }

            _list.DisplayMemberPath = nameof(AttachmentItem.Display);
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
            if (_list.SelectedItem is AttachmentItem item && File.Exists(item.FilePath))
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }
    }
}
