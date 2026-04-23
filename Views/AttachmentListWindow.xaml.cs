using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class AttachmentListWindow : Window
    {
        private readonly IDatabaseService _dbService;
        private readonly ObservableCollection<AttachmentRecord> _attachments;
        private readonly bool _allowDelete;
        private readonly Action? _onListChanged;
        private bool _isBusy;

        public Visibility DeleteButtonVisibility => _allowDelete ? Visibility.Visible : Visibility.Collapsed;

        public AttachmentListWindow(IEnumerable<AttachmentRecord> attachments, IDatabaseService dbService, bool allowDelete = true, string? headerText = null, Action? onListChanged = null)
        {
            _allowDelete = allowDelete;
            InitializeComponent();
            _dbService = dbService;
            _onListChanged = onListChanged;
            _attachments = new ObservableCollection<AttachmentRecord>();
            ListAttachments.ItemsSource = _attachments;
            ApplyStaticButtonIcons();
            WindowStateService.Restore(this, nameof(AttachmentListWindow));
            Closing += AttachmentListWindow_Closing;

            RefreshAttachments(attachments, headerText);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AttachmentRecord att)
            {
                try
                {
                    if (att.Exists)
                    {
                        Process.Start(new ProcessStartInfo(att.FilePath) { UseShellExecute = true });
                        SetLocalStatus(
                            ExternalOpenFeedbackFormatter.BuildOpenedFileStatusOrFallback(
                                "الملف الداعم",
                                att.FilePath,
                                att.OriginalFileName),
                            ShellStatusTone.Info,
                            mirrorToShell: true);
                    }
                    else
                    {
                        SetLocalStatus("الملف غير موجود في المسار المحدد. ربما تم حذفه يدويًا.", ShellStatusTone.Warning, mirrorToShell: true);
                        AppDialogService.ShowWarning("الملف غير موجود في المسار المحدد. ربما تم حذفه يدويًا.", "ملف مفقود");
                    }
                }
                catch (Exception ex)
                {
                    SetLocalStatus("تعذر فتح الملف المحدد.", ShellStatusTone.Error, mirrorToShell: true);
                    AppDialogService.ShowError(ex, "تعذر فتح الملف المحدد.");
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            if (sender is Button btn && btn.Tag is AttachmentRecord att &&
                AppDialogService.ConfirmDelete("المرفق", att.OriginalFileName, "سيتم حذفه من قاعدة البيانات ومن القرص إذا لم يكن مستخدمًا في إصدار آخر."))
            {
                try
                {
                    SetBusy(true, $"جاري حذف المرفق {att.OriginalFileName}...");
                    await Task.Run(() => _dbService.DeleteAttachment(att));
                    _attachments.Remove(att);
                    UpdateAttachmentState();

                    if (!_attachments.Any())
                    {
                        TxtHeader.Text = "لا توجد مرفقات لهذا الضمان";
                    }

                    _onListChanged?.Invoke();
                    SetLocalStatus($"تم حذف المرفق {att.OriginalFileName}.", ShellStatusTone.Success, mirrorToShell: true);
                    AppDialogService.ShowSuccess($"تم حذف المرفق {att.OriginalFileName} بنجاح.");
                }
                catch (Exception ex)
                {
                    SetLocalStatus("فشلت عملية حذف المرفق.", ShellStatusTone.Error, mirrorToShell: true);
                    AppDialogService.ShowError(ex, "تعذر حذف المرفق المحدد.");
                }
                finally
                {
                    SetBusy(false);
                }
            }
        }

        public void RefreshAttachments(IEnumerable<AttachmentRecord> attachments, string? headerText = null)
        {
            _attachments.Clear();
            foreach (AttachmentRecord attachment in attachments)
            {
                _attachments.Add(attachment);
            }

            if (!string.IsNullOrWhiteSpace(headerText))
            {
                TxtHeader.Text = headerText;
            }

            if (!_allowDelete)
            {
                Title = "استعراض المرفقات";
            }

            if (!_attachments.Any())
            {
                TxtHeader.Text = headerText ?? "لا توجد مرفقات لهذا الضمان";
            }

            UpdateAttachmentState();
        }

        private MainWindow? GetShell()
        {
            return Application.Current.MainWindow as MainWindow;
        }

        private void SetBusy(bool isBusy, string? statusMessage = null)
        {
            _isBusy = isBusy;
            ListAttachments.IsEnabled = !isBusy;
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                SetLocalStatus(statusMessage, ShellStatusTone.Info, mirrorToShell: true);
            }
        }

        private void SetLocalStatus(string message, ShellStatusTone tone = ShellStatusTone.Info, bool mirrorToShell = false)
        {
            WindowLocalFeedbackPresenter.Show(
                LocalStatusBorder,
                TxtLocalStatus,
                message,
                tone,
                mirrorToShell ? static (statusMessage, statusTone) => ((MainWindow?)Application.Current.MainWindow)?.SetStatus(statusMessage, statusTone) : null);
        }

        private void AttachmentListWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowStateService.Save(this, nameof(AttachmentListWindow));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UpdateAttachmentState()
        {
            bool hasAttachments = _attachments.Any();
            ListAttachments.Visibility = hasAttachments ? Visibility.Visible : Visibility.Collapsed;
            EmptyStatePanel.Visibility = hasAttachments ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ApplyStaticButtonIcons()
        {
            ButtonIconContentFactory.Apply(BtnClose, "Icon_Geometry_Close", "إغلاق");
        }
    }
}
