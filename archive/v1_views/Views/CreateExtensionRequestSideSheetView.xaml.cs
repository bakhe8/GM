using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class CreateExtensionRequestSideSheetView : UserControl
    {
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly Action<WorkflowRequest> _onRequestCreated;
        private readonly Action<int>? _onOpenExistingRequest;
        private Guarantee _guarantee;
        private WorkflowRequestListItem? _pendingExtensionRequest;

        public CreateExtensionRequestSideSheetView(
            Guarantee guarantee,
            IDatabaseService databaseService,
            IWorkflowService workflowService,
            Action<WorkflowRequest> onRequestCreated,
            Action<int>? onOpenExistingRequest = null)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _workflowService = workflowService;
            _onRequestCreated = onRequestCreated;
            _onOpenExistingRequest = onOpenExistingRequest;
            _guarantee = guarantee;

            TxtCreatedBy.Text = Environment.UserName;
            ToolTipService.SetShowOnDisabled(BtnSave, true);
            LoadView();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private void LoadView()
        {
            _guarantee = _databaseService.GetGuaranteeById(_guarantee.Id) ?? _guarantee;
            _pendingExtensionRequest = LoadPendingExtensionRequest();

            TxtGuaranteeIdentity.Text = $"{_guarantee.GuaranteeNo} | {_guarantee.Supplier}";
            TxtGuaranteeSummary.Text = $"{_guarantee.Amount:N2} | ينتهي {_guarantee.ExpiryDate:yyyy-MM-dd} | {_guarantee.StatusLabel} | {_guarantee.LifecycleStatusLabel}";
            TxtCurrentAmount.Text = _guarantee.Amount.ToString("N2");
            TxtCurrentExpiry.Text = _guarantee.ExpiryDate.ToString("yyyy-MM-dd");
            TxtReasonSummary.Text = ExtensionRequestFlowSupport.BuildReasonSummary(_guarantee);

            if (!DateRequestedExpiry.SelectedDate.HasValue || DateRequestedExpiry.SelectedDate.Value.Date <= _guarantee.ExpiryDate.Date)
            {
                DateRequestedExpiry.SelectedDate = ExtensionRequestFlowSupport.GetSuggestedRequestedExpiryDate(_guarantee);
            }

            if (_pendingExtensionRequest != null)
            {
                PendingRequestPanel.Visibility = Visibility.Visible;
                TxtPendingRequestSummary.Text =
                    $"يوجد بالفعل طلب تمديد معلق برقم {_pendingExtensionRequest.Request.SequenceNumber} منذ {_pendingExtensionRequest.RequestDateLabel}. يمكنك فتحه بدل إنشاء طلب جديد.";
            }
            else
            {
                PendingRequestPanel.Visibility = Visibility.Collapsed;
            }

            UpdateEffectPreview();
            UpdateSaveAvailability();
        }

        private WorkflowRequestListItem? LoadPendingExtensionRequest()
        {
            int rootId = _guarantee.RootId ?? _guarantee.Id;
            return _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = rootId,
                RequestType = RequestType.Extension,
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending,
                Limit = 1
            }).FirstOrDefault();
        }

        private void RequestedExpiry_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateEffectPreview();
            UpdateSaveAvailability();
        }

        private void CreatedBy_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateSaveAvailability();
        }

        private void UpdateEffectPreview()
        {
            TxtEffectPreview.Text = ExtensionRequestFlowSupport.BuildEffectPreview(_guarantee, DateRequestedExpiry.SelectedDate);
        }

        private void UpdateSaveAvailability()
        {
            bool canSave = IsSaveReady(out string reason);
            BtnSave.IsEnabled = canSave;
            BtnSave.ToolTip = canSave
                ? "سيُنشأ طلب تمديد معلق ويُحدَّث ملف الضمان مباشرة دون مغادرة السياق."
                : $"غير متاح الآن - {reason}";
            TxtSaveGuidance.Text = canSave
                ? "الطلب جاهز للحفظ من نفس سياق ملف الضمان."
                : reason;
        }

        private bool IsSaveReady(out string reason)
        {
            if (_pendingExtensionRequest != null)
            {
                reason = "يوجد بالفعل طلب تمديد معلق لهذا الضمان.";
                return false;
            }

            return ExtensionRequestFlowSupport.TryValidate(
                _guarantee,
                DateRequestedExpiry.SelectedDate,
                TxtCreatedBy.Text,
                out reason);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MainWindow? shell = GetShell();
            if (!IsSaveReady(out string reason))
            {
                AppDialogService.ShowWarning(reason);
                shell?.SetStatus("تعذر إنشاء طلب التمديد.", ShellStatusTone.Warning);
                return;
            }

            try
            {
                WorkflowRequest request = _workflowService.CreateExtensionRequest(
                    _guarantee.Id,
                    DateRequestedExpiry.SelectedDate!.Value.Date,
                    TxtNotes.Text.Trim(),
                    TxtCreatedBy.Text.Trim());

                shell?.CloseSideSheet();
                _onRequestCreated(request);
                shell?.SetStatus($"تم إنشاء طلب التمديد للضمان رقم {_guarantee.GuaranteeNo}.", ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر إنشاء طلب التمديد.", ShellStatusTone.Warning);
                LoadView();
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر إنشاء طلب التمديد.");
                shell?.SetStatus("فشل إنشاء طلب التمديد.", ShellStatusTone.Error);
            }
        }

        private void OpenExistingRequest_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingExtensionRequest == null)
            {
                return;
            }

            GetShell()?.CloseSideSheet();
            _onOpenExistingRequest?.Invoke(_pendingExtensionRequest.Request.Id);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.CloseSideSheet();
        }
    }
}
