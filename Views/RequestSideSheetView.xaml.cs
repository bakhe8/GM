using System;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Views
{
    public partial class RequestSideSheetView : UserControl
    {
        private readonly WorkflowRequestListItem _item;
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;

        public RequestSideSheetView(
            WorkflowRequestListItem item,
            IDatabaseService databaseService,
            IWorkflowService workflowService)
        {
            InitializeComponent();
            _item = item;
            _databaseService = databaseService;
            _workflowService = workflowService;
            LoadView();
        }

        private void LoadView()
        {
            TxtRequestTitle.Text = $"الطلب {_item.Request.SequenceNumber}";
            TxtRequestSummary.Text = $"{_item.GuaranteeNo} | {_item.Supplier} | {_item.Bank}";
            TxtRequestStatus.Text = _item.Request.StatusLabel;
            TxtRequestType.Text = _item.Request.TypeLabel;
            TxtRequestDate.Text = _item.RequestDateLabel;
            TxtCurrentValue.Text = _item.CurrentValueDisplay;
            TxtRequestedValue.Text = _item.RequestedValueDisplay;
            TxtNotes.Text = string.IsNullOrWhiteSpace(_item.Request.Notes) ? "لا توجد ملاحظات مضافة." : _item.Request.Notes;
            BtnOpenLetter.IsEnabled = _item.Request.HasLetter;
            BtnResponseAction.Content = "مستند الرد";
            BtnResponseAction.IsEnabled = true;
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private void OpenGuarantee_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = _databaseService.GetGuaranteeById(_item.CurrentGuaranteeId);
            if (guarantee == null)
            {
                GetShell()?.SetStatus("تعذر فتح ملف الضمان المرتبط بالطلب.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(guarantee, "لوح الطلبات", true, requestIdToFocus: _item.Request.Id);
        }

        private void OpenLetter_Click(object sender, RoutedEventArgs e)
        {
            if (!_item.Request.HasLetter)
            {
                GetShell()?.SetStatus("لا يوجد خطاب طلب محفوظ لهذا السجل.", ShellStatusTone.Warning);
                return;
            }

            try
            {
                _workflowService.OpenRequestLetter(_item.Request);
                GetShell()?.SetStatus("تم فتح خطاب الطلب.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح خطاب الطلب.");
                GetShell()?.SetStatus("فشل فتح خطاب الطلب.", ShellStatusTone.Error);
            }
        }

        private void OpenResponse_Click(object sender, RoutedEventArgs e)
        {
            if (_item.Request.HasResponseDocument)
            {
                try
                {
                    _workflowService.OpenResponseDocument(_item.Request);
                    GetShell()?.SetStatus("تم فتح مستند الرد.", ShellStatusTone.Info);
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(ex, "تعذر فتح مستند الرد.");
                    GetShell()?.SetStatus("فشل فتح مستند الرد.", ShellStatusTone.Error);
                }

                return;
            }

            if (_item.Request.Status == RequestStatus.Pending)
            {
                OpenPendingResponseDialog();
                return;
            }

            if (!CanAttachResponseDocument())
            {
                GetShell()?.SetStatus("تعذر استخدام إجراء مستند الرد لهذا الطلب.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            AttachWorkflowResponseDocumentWindow dialog = new(_item);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            try
            {
                _workflowService.AttachResponseDocumentToClosedRequest(
                    _item.Request.Id,
                    dialog.ResponseDocumentPath,
                    dialog.AdditionalNotes);

                shell?.ShowOperationCenter(true, _item.Request.Id);
                shell?.SetStatus("تم إلحاق مستند رد البنك بهذا الطلب.", ShellStatusTone.Success);
            }
            catch (DeferredFilePromotionException ex)
            {
                shell?.ShowOperationCenter(true, _item.Request.Id);
                AppDialogService.ShowWarning(ex.UserMessage);
                shell?.SetStatus("تم تحديث الطلب، لكن بعض الملفات ما زالت بانتظار الاستكمال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر إلحاق مستند الرد.");
                shell?.SetStatus("فشل إلحاق مستند الرد.", ShellStatusTone.Error);
            }
        }

        private void OpenPendingResponseDialog()
        {
            MainWindow? shell = GetShell();
            RecordWorkflowResponseWindow dialog = new(_item);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            string? responsePath = string.IsNullOrWhiteSpace(dialog.ResponseDocumentPath)
                ? null
                : dialog.ResponseDocumentPath;

            try
            {
                _workflowService.RecordBankResponse(
                    _item.Request.Id,
                    dialog.SelectedStatus,
                    dialog.ResponseNotes,
                    responsePath,
                    dialog.PromoteResponseDocumentToOfficialAttachment);

                shell?.ShowOperationCenter(true, _item.Request.Id);
                shell?.SetStatus("تم تسجيل رد البنك وتحديث الطلب.", ShellStatusTone.Success);
            }
            catch (DeferredFilePromotionException ex)
            {
                shell?.ShowOperationCenter(true, _item.Request.Id);
                AppDialogService.ShowWarning(ex.UserMessage);
                shell?.SetStatus("تم تحديث الطلب، لكن بعض الملفات ما زالت بانتظار الاستكمال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل رد البنك.");
                shell?.SetStatus("فشل تسجيل رد البنك.", ShellStatusTone.Error);
            }
        }

        private bool CanAttachResponseDocument()
        {
            return !_item.Request.HasResponseDocument;
        }
    }
}
