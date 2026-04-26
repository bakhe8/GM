using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using GuaranteeManager.ViewModels;

namespace GuaranteeManager.Views
{
    public partial class TodayDeskView : UserControl, IRefreshableView
    {
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly IOperationalInquiryService _inquiryService;
        private readonly IContextActionService _contextActionService;
        private readonly TodayDeskViewModel _viewModel;
        private readonly SecondaryWindowManager _windowManager = SecondaryWindowManager.Instance;

        public TodayDeskView(
            IDatabaseService databaseService,
            IWorkflowService workflowService,
            IExcelService excelService,
            IOperationalInquiryService inquiryService,
            IContextActionService contextActionService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _workflowService = workflowService;
            _excelService = excelService;
            _inquiryService = inquiryService;
            _contextActionService = contextActionService;
            _viewModel = new TodayDeskViewModel(databaseService, workflowService);
            DataContext = _viewModel;
            Loaded += (_, _) => RefreshView();
            IsVisibleChanged += TodayDeskView_IsVisibleChanged;
        }

        private void TodayDeskView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshGrid(GridUrgentGuarantees);
                    RefreshGrid(GridPendingRequests);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void RefreshGrid(DataGrid grid)
        {
            grid.UpdateLayout();
            foreach (var column in grid.Columns)
            {
                var originalWidth = column.Width;
                column.Width = 0;
                column.Width = originalWidth;
            }
        }

        public void RefreshView()
        {
            _viewModel.Refresh();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private Guarantee? SelectedUrgentGuarantee => _viewModel.SelectedUrgentGuarantee;

        private WorkflowRequestListItem? SelectedPendingRequest => _viewModel.SelectedPendingRequest;

        private void OpenUrgentGuarantee_Click(object sender, RoutedEventArgs e)
        {
            if (GridUrgentGuarantees.SelectedItem is not Guarantee guarantee)
            {
                GetShell()?.SetStatus("حدد ضمانًا أولاً.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(guarantee, "المتابعات", true);
        }

        private void OpenPendingRequest_Click(object sender, RoutedEventArgs e)
        {
            if (GridPendingRequests.SelectedItem is not WorkflowRequestListItem item)
            {
                GetShell()?.SetStatus("حدد طلبًا أولاً.", ShellStatusTone.Warning);
                return;
            }

            Guarantee? guarantee = _databaseService.GetGuaranteeById(item.CurrentGuaranteeId);
            if (guarantee == null)
            {
                GetShell()?.SetStatus("تعذر الوصول إلى ملف الضمان المرتبط بالطلب.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(guarantee, "المتابعات", true, requestIdToFocus: item.Request.Id);
        }

        private void NewGuarantee_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowAddEntryScreen(true);
        }

        private void OpenPortfolio_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowDataTable();
        }

        private void OpenRequests_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowOperationCenter();
        }

        private void GridUrgentGuarantees_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenUrgentGuarantee_Click(sender, new RoutedEventArgs());
        }

        private void GridPendingRequests_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenPendingRequest_Click(sender, new RoutedEventArgs());
        }

        private void GridUrgentGuarantees_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridContextMenuAssist.TrySelectRowFromRightClick(GridUrgentGuarantees, e);
        }

        private void GridUrgentGuarantees_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SelectedUrgentGuarantee == null || !DataGridContextMenuAssist.IsPointerOverRow(GridUrgentGuarantees))
            {
                GridUrgentGuarantees.ContextMenu = null;
                e.Handled = true;
                return;
            }

            GridUrgentGuarantees.ContextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildGuaranteeSections(_contextActionService, includeVisibleListExport: false),
                ResolveUrgentGuaranteeContextActionHandler,
                ResolveUrgentGuaranteeContextActionAvailability);
        }

        private void GridPendingRequests_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridContextMenuAssist.TrySelectRowFromRightClick(GridPendingRequests, e);
        }

        private void GridPendingRequests_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SelectedPendingRequest == null || !DataGridContextMenuAssist.IsPointerOverRow(GridPendingRequests))
            {
                GridPendingRequests.ContextMenu = null;
                e.Handled = true;
                return;
            }

            GridPendingRequests.ContextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildRequestSections(_contextActionService),
                ResolvePendingRequestContextActionHandler,
                ResolvePendingRequestContextActionAvailability);
        }

        private RoutedEventHandler? ResolveUrgentGuaranteeContextActionHandler(string actionId)
        {
            return actionId switch
            {
                "workspace.guarantee.open-file" => OpenUrgentGuarantee_Click,
                "execute.create-extension" => CreateUrgentGuaranteeExtensionRequest_Click,
                "execute.create-reduction" => CreateUrgentGuaranteeReductionRequest_Click,
                "execute.create-release" => CreateUrgentGuaranteeReleaseRequest_Click,
                "execute.create-liquidation" => CreateUrgentGuaranteeLiquidationRequest_Click,
                "execute.create-verification" => CreateUrgentGuaranteeVerificationRequest_Click,
                "execute.create-replacement" => CreateUrgentGuaranteeReplacementRequest_Click,
                "execute.create-annulment" => CreateUrgentGuaranteeAnnulmentRequest_Click,
                "execute.edit-guarantee" => EditUrgentGuarantee_Click,
                "navigate.history" => OpenUrgentGuaranteeHistory_Click,
                "evidence.attachments" => OpenUrgentGuaranteeAttachments_Click,
                "export.guarantee-report" => ExportUrgentGuaranteeReport_Click,
                "export.guarantee-history" => ExportUrgentGuaranteeHistory_Click,
                "export.same-bank" => ExportUrgentGuaranteesByBank_Click,
                "export.same-supplier" => ExportUrgentGuaranteesBySupplier_Click,
                "export.same-temporal-status" => ExportUrgentGuaranteesByTemporalStatus_Click,
                _ when GuaranteeInquiryActionSupport.IsInquiryAction(actionId) => (_, _) => OpenUrgentGuaranteeInquiry(actionId),
                "copy.guarantee-no" => CopyUrgentGuaranteeNumber_Click,
                "copy.supplier" => CopyUrgentGuaranteeSupplier_Click,
                "copy.reference-type" => CopyUrgentGuaranteeReferenceType_Click,
                "copy.reference-number" => CopyUrgentGuaranteeReferenceNumber_Click,
                _ => null
            };
        }

        private ContextActionAvailability ResolveUrgentGuaranteeContextActionAvailability(ContextActionDefinition action)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null)
            {
                return ContextActionAvailability.Disabled("حدد سجلًا أولًا.");
            }

            if (GuaranteeInquiryActionSupport.IsInquiryAction(action.Id ?? string.Empty))
            {
                return GuaranteeInquiryActionSupport.GetAvailability(action.Id ?? string.Empty, guarantee);
            }

            return action.Id switch
            {
                "evidence.attachments" when guarantee.Attachments.Count == 0 => ContextActionAvailability.Disabled("لا توجد مرفقات لهذا الضمان."),
                "export.same-bank" when string.IsNullOrWhiteSpace(guarantee.Bank) => ContextActionAvailability.Disabled("لا يوجد بنك مرتبط بهذا السجل."),
                "export.same-supplier" when string.IsNullOrWhiteSpace(guarantee.Supplier) => ContextActionAvailability.Disabled("لا يوجد مورد مرتبط بهذا السجل."),
                "execute.create-extension" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active => ContextActionAvailability.Disabled("طلب التمديد متاح للضمانات النشطة فقط."),
                "execute.create-extension" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Extension) => ContextActionAvailability.Disabled("يوجد بالفعل طلب تمديد معلق لهذا الضمان."),
                "execute.create-reduction" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.Amount <= 0 => ContextActionAvailability.Disabled("طلب التخفيض متاح للضمانات النشطة التي يزيد مبلغها على صفر."),
                "execute.create-reduction" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Reduction) => ContextActionAvailability.Disabled("يوجد بالفعل طلب تخفيض معلق لهذا الضمان."),
                "execute.create-release" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active => ContextActionAvailability.Disabled("طلب الإفراج متاح للضمانات النشطة فقط."),
                "execute.create-release" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Release) => ContextActionAvailability.Disabled("يوجد بالفعل طلب إفراج معلق لهذا الضمان."),
                "execute.create-liquidation" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active => ContextActionAvailability.Disabled("طلب التسييل متاح للضمانات النشطة فقط."),
                "execute.create-liquidation" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Liquidation) => ContextActionAvailability.Disabled("يوجد بالفعل طلب تسييل معلق لهذا الضمان."),
                "execute.create-verification" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active => ContextActionAvailability.Disabled("طلب التحقق متاح للضمانات النشطة فقط."),
                "execute.create-verification" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Verification) => ContextActionAvailability.Disabled("يوجد بالفعل طلب تحقق معلق لهذا الضمان."),
                "execute.create-replacement" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active => ContextActionAvailability.Disabled("طلب الاستبدال متاح للضمانات النشطة فقط."),
                "execute.create-replacement" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Replacement) => ContextActionAvailability.Disabled("يوجد بالفعل طلب استبدال معلق لهذا الضمان."),
                "execute.create-annulment" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Released && guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Liquidated => ContextActionAvailability.Disabled("طلب النقض متاح للضمانات المفرج عنها أو المسيّلة فقط."),
                "execute.create-annulment" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Annulment) => ContextActionAvailability.Disabled("يوجد بالفعل طلب نقض معلق لهذا الضمان."),
                "copy.guarantee-no" when string.IsNullOrWhiteSpace(guarantee.GuaranteeNo) => ContextActionAvailability.Disabled("رقم الضمان غير متاح."),
                "copy.supplier" when string.IsNullOrWhiteSpace(guarantee.Supplier) => ContextActionAvailability.Disabled("اسم المورد غير متاح."),
                "copy.reference-number" when string.IsNullOrWhiteSpace(guarantee.ReferenceNumber) => ContextActionAvailability.Disabled("رقم المرجع غير متاح."),
                _ => ContextActionAvailability.Enabled()
            };
        }

        private void OpenUrgentGuaranteeInquiry(string actionId)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;

            try
            {
                OperationalInquiryResult? result = GuaranteeInquiryActionSupport.Execute(
                    actionId,
                    guarantee,
                    _inquiryService,
                    PromptForEmployeeName);

                if (result == null)
                {
                    return;
                }

                var inquiryWindow = new InquiryResultWindow(result, _databaseService, _workflowService, _excelService);
                if (shell != null)
                {
                    inquiryWindow.Owner = shell;
                }

                inquiryWindow.Show();
                shell?.SetStatus($"تم فتح جواب: {result.Title}.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح جواب هذا الاستعلام.");
                shell?.SetStatus("فشل فتح جواب هذا الاستعلام.", ShellStatusTone.Error);
            }
        }

        private string? PromptForEmployeeName()
        {
            MainWindow? shell = GetShell();
            var dialog = new TextPromptWindow(
                "استعلام أداء موظف",
                "أدخل اسم الموظف الذي تريد مراجعة طلباته خلال الشهر الماضي على العقود.",
                "اسم الموظف",
                "عرض الجواب",
                nextStepHint: "سيعرض النظام عدد طلبات التمديد أو الإفراج التي أنشأها هذا الموظف خلال الشهر الماضي للعقود.");

            if (shell != null)
            {
                dialog.Owner = shell;
            }

            return dialog.ShowDialog() == true ? dialog.ResultText : null;
        }

        private void CreateUrgentGuaranteeExtensionRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التمديد متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Extension))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تمديد معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تمديد معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateExtensionRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateExtensionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedExpiryDate,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التمديد للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التمديد.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التمديد.");
                shell?.SetStatus("فشل تسجيل طلب التمديد.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeReductionRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.Amount <= 0)
            {
                shell?.SetStatus("طلب التخفيض متاح للضمانات النشطة التي يزيد مبلغها على صفر.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Reduction))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تخفيض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تخفيض معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReductionRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReductionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedAmount,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التخفيض للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التخفيض.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التخفيض.");
                shell?.SetStatus("فشل تسجيل طلب التخفيض.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeReleaseRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب الإفراج متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Release))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب إفراج معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب إفراج معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReleaseRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReleaseRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب الإفراج للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب الإفراج.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب الإفراج.");
                shell?.SetStatus("فشل تسجيل طلب الإفراج.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeLiquidationRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التسييل متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Liquidation))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تسييل معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تسييل معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateLiquidationRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateLiquidationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التسييل للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التسييل.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التسييل.");
                shell?.SetStatus("فشل تسجيل طلب التسييل.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeVerificationRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التحقق متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Verification))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تحقق معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تحقق معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateVerificationRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateVerificationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التحقق للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التحقق.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التحقق.");
                shell?.SetStatus("فشل تسجيل طلب التحقق.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeReplacementRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب الاستبدال متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Replacement))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب استبدال معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب استبدال معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReplacementRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReplacementRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.ReplacementGuaranteeNo,
                    dialog.ReplacementSupplier,
                    dialog.ReplacementBank,
                    dialog.ReplacementAmount,
                    dialog.ReplacementExpiryDate,
                    dialog.ReplacementGuaranteeType,
                    dialog.ReplacementBeneficiary,
                    dialog.ReplacementReferenceType,
                    dialog.ReplacementReferenceNumber,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب الاستبدال للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب الاستبدال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب الاستبدال.");
                shell?.SetStatus("فشل تسجيل طلب الاستبدال.", ShellStatusTone.Error);
            }
        }

        private void CreateUrgentGuaranteeAnnulmentRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Released &&
                guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Liquidated)
            {
                shell?.SetStatus("طلب النقض متاح للضمانات المفرج عنها أو المسيّلة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Annulment))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب نقض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب نقض معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateAnnulmentRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateAnnulmentRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestReason,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب النقض للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب النقض.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب النقض.");
                shell?.SetStatus("فشل تسجيل طلب النقض.", ShellStatusTone.Error);
            }
        }

        private void EditUrgentGuarantee_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            shell?.ShowEditGuarantee(guarantee, GuaranteeFormReturnTarget.TodayDesk);
        }

        private RoutedEventHandler? ResolvePendingRequestContextActionHandler(string actionId)
        {
            return actionId switch
            {
                "request.record-response" => RecordPendingRequestResponse_Click,
                "request.open-current-guarantee" => OpenPendingRequest_Click,
                "request.open-history" => OpenPendingRequestHistory_Click,
                "request.open-letter" => OpenPendingRequestLetter_Click,
                "request.open-response" => OpenPendingRequestResponse_Click,
                "request.export-pending-same-type" => ExportPendingRequestsSameType_Click,
                "request.export-pending-extension" => ExportPendingExtensionRequests_Click,
                "request.export-pending-reduction" => ExportPendingReductionRequests_Click,
                "request.export-pending-release" => ExportPendingReleaseRequests_Click,
                "request.export-pending-liquidation" => ExportPendingLiquidationRequests_Click,
                "request.export-pending-verification" => ExportPendingVerificationRequests_Click,
                "request.export-pending-replacement" => ExportPendingReplacementRequests_Click,
                "workspace.request.copy-guarantee-no" => CopyPendingRequestGuaranteeNumber_Click,
                "workspace.request.copy-supplier" => CopyPendingRequestSupplier_Click,
                _ => null
            };
        }

        private ContextActionAvailability ResolvePendingRequestContextActionAvailability(ContextActionDefinition action)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            if (item == null)
            {
                return ContextActionAvailability.Disabled("حدد طلبًا أولًا.");
            }

            return action.Id switch
            {
                "request.record-response" when item.Request.Status != RequestStatus.Pending =>
                    ContextActionAvailability.Disabled("تسجيل الاستجابة متاح للطلبات المعلقة فقط."),
                "request.open-letter" when item.Request.HasLetter != true =>
                    ContextActionAvailability.Disabled("لا يوجد خطاب طلب محفوظ لهذا السجل."),
                "request.open-response" when item.Request.HasResponseDocument != true =>
                    ContextActionAvailability.Disabled("لا يوجد مستند رد محفوظ لهذا السجل."),
                "request.open-current-guarantee" when _databaseService.GetGuaranteeById(item.CurrentGuaranteeId) == null =>
                    ContextActionAvailability.Disabled("تعذر العثور على ملف الضمان المرتبط."),
                "request.open-history" when _databaseService.GetGuaranteeById(item.CurrentGuaranteeId) == null =>
                    ContextActionAvailability.Disabled("تعذر العثور على ملف الضمان المرتبط."),
                "request.export-pending-same-type" when !_viewModel.GetPendingRequestsByType(item.Request.Type).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات معلقة من نفس النوع حاليًا."),
                "request.export-pending-extension" when !_viewModel.GetPendingRequestsByType(RequestType.Extension).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تمديد معلقة حاليًا."),
                "request.export-pending-reduction" when !_viewModel.GetPendingRequestsByType(RequestType.Reduction).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تخفيض معلقة حاليًا."),
                "request.export-pending-release" when !_viewModel.GetPendingRequestsByType(RequestType.Release).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات إفراج معلقة حاليًا."),
                "request.export-pending-liquidation" when !_viewModel.GetPendingRequestsByType(RequestType.Liquidation).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تسييل معلقة حاليًا."),
                "request.export-pending-verification" when !_viewModel.GetPendingRequestsByType(RequestType.Verification).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تحقق معلقة حاليًا."),
                "request.export-pending-replacement" when !_viewModel.GetPendingRequestsByType(RequestType.Replacement).Any() =>
                    ContextActionAvailability.Disabled("لا توجد طلبات استبدال معلقة حاليًا."),
                "workspace.request.copy-guarantee-no" when string.IsNullOrWhiteSpace(item.GuaranteeNo) =>
                    ContextActionAvailability.Disabled("رقم الضمان غير متاح."),
                "workspace.request.copy-supplier" when string.IsNullOrWhiteSpace(item.Supplier) =>
                    ContextActionAvailability.Disabled("اسم المورد غير متاح."),
                _ => ContextActionAvailability.Enabled()
            };
        }

        private void OpenUrgentGuaranteeHistory_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null)
            {
                return;
            }

            bool opened = _windowManager.ShowOrActivate(
                $"history:{guarantee.RootId ?? guarantee.Id}",
                () => new GuaranteeHistoryWindow(guarantee, _databaseService),
                existing =>
                {
                    if (existing is GuaranteeHistoryWindow historyWindow)
                    {
                        historyWindow.RefreshHistory();
                    }
                });

            GetShell()?.SetStatus(
                opened ? $"تم فتح سجل الضمان رقم {guarantee.GuaranteeNo}." : $"تم تنشيط سجل الضمان رقم {guarantee.GuaranteeNo}.",
                ShellStatusTone.Info);
        }

        private void OpenUrgentGuaranteeAttachments_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            MainWindow? shell = GetShell();
            if (guarantee == null || shell == null || guarantee.Attachments.Count == 0)
            {
                return;
            }

            shell.OpenAttachmentWindow(
                $"attachments:readonly:{guarantee.Id}",
                () => new AttachmentListWindow(
                    guarantee.Attachments,
                    _databaseService,
                    allowDelete: false,
                    headerText: $"مرفقات الضمان رقم {guarantee.GuaranteeNo}"),
                $"تم فتح مرفقات الضمان رقم {guarantee.GuaranteeNo}.",
                $"تم تنشيط مرفقات الضمان رقم {guarantee.GuaranteeNo}.",
                existing =>
                {
                    if (existing is AttachmentListWindow attachmentWindow)
                    {
                        attachmentWindow.RefreshAttachments(
                            guarantee.Attachments,
                            $"مرفقات الضمان رقم {guarantee.GuaranteeNo}");
                    }
                });
        }

        private void ExportUrgentGuaranteeReport_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null)
            {
                return;
            }

            bool exported = _excelService.ExportSingleGuaranteeReport(guarantee);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportUrgentGuaranteeHistory_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null)
            {
                return;
            }

            var historyReportService = new GuaranteeHistoryReportService();
            List<Guarantee> history = _databaseService.GetGuaranteeHistory(guarantee.Id)
                .OrderByDescending(item => item.VersionNumber)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();

            bool exported = historyReportService.ExportHistoryToExcel(guarantee, history);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportUrgentGuaranteesByBank_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null || string.IsNullOrWhiteSpace(guarantee.Bank))
            {
                return;
            }

            bool exported = _excelService.ExportGuaranteesByBank(
                guarantee.Bank,
                _viewModel.GetGuaranteesByBank(guarantee.Bank));

            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع ضمانات البنك {guarantee.Bank}."
                    : $"تم إلغاء تصدير ضمانات البنك {guarantee.Bank}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportUrgentGuaranteesBySupplier_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null || string.IsNullOrWhiteSpace(guarantee.Supplier))
            {
                return;
            }

            bool exported = _excelService.ExportGuaranteesBySupplier(
                guarantee.Supplier,
                _viewModel.GetGuaranteesBySupplier(guarantee.Supplier));

            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع ضمانات المورد {guarantee.Supplier}."
                    : $"تم إلغاء تصدير ضمانات المورد {guarantee.Supplier}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportUrgentGuaranteesByTemporalStatus_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            if (guarantee == null || !TryGetTemporalStatus(guarantee, out GuaranteeTimeStatus temporalStatus))
            {
                GetShell()?.SetStatus("تعذر تحديد الحالة الزمنية لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            bool exported = _excelService.ExportGuaranteesByTemporalStatus(
                guarantee.StatusLabel,
                _viewModel.GetGuaranteesByTemporalStatus(temporalStatus));

            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع الضمانات ذات الحالة الزمنية {guarantee.StatusLabel}."
                    : $"تم إلغاء تصدير الضمانات ذات الحالة الزمنية {guarantee.StatusLabel}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void RecordPendingRequestResponse_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            MainWindow? shell = GetShell();
            if (item == null)
            {
                return;
            }

            var dialog = new RecordWorkflowResponseWindow(item);
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
                    item.Request.Id,
                    dialog.SelectedStatus,
                    dialog.ResponseNotes,
                    responsePath,
                    dialog.PromoteResponseDocumentToOfficialAttachment);

                RefreshView();
                shell?.SetStatus("تم تسجيل رد البنك وتحديث الطلب.", ShellStatusTone.Success);
            }
            catch (DeferredFilePromotionException ex)
            {
                RefreshView();
                AppDialogService.ShowWarning(ex.UserMessage);
                shell?.SetStatus("تم تسجيل رد البنك، لكن بعض الملفات ما زالت بانتظار الاستكمال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل رد البنك.");
                shell?.SetStatus("فشل تسجيل رد البنك.", ShellStatusTone.Error);
            }
        }

        private void OpenPendingRequestHistory_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            if (item == null)
            {
                return;
            }

            Guarantee? guarantee = _databaseService.GetGuaranteeById(item.CurrentGuaranteeId);
            if (guarantee == null)
            {
                GetShell()?.SetStatus("تعذر العثور على ملف الضمان المرتبط.", ShellStatusTone.Warning);
                return;
            }

            bool opened = _windowManager.ShowOrActivate(
                $"history:{guarantee.RootId ?? guarantee.Id}",
                () => new GuaranteeHistoryWindow(guarantee, _databaseService),
                existing =>
                {
                    if (existing is GuaranteeHistoryWindow historyWindow)
                    {
                        historyWindow.RefreshHistory();
                    }
                });

            GetShell()?.SetStatus(
                opened ? $"تم فتح سجل الضمان رقم {guarantee.GuaranteeNo}." : $"تم تنشيط سجل الضمان رقم {guarantee.GuaranteeNo}.",
                ShellStatusTone.Info);
        }

        private void OpenPendingRequestLetter_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            if (item?.Request.HasLetter != true)
            {
                return;
            }

            _workflowService.OpenRequestLetter(item.Request);
            GetShell()?.SetStatus("تم فتح خطاب الطلب.", ShellStatusTone.Info);
        }

        private void OpenPendingRequestResponse_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            if (item?.Request.HasResponseDocument != true)
            {
                return;
            }

            _workflowService.OpenResponseDocument(item.Request);
            GetShell()?.SetStatus("تم فتح مستند الرد.", ShellStatusTone.Info);
        }

        private void ExportPendingRequestsSameType_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedPendingRequest;
            if (item == null)
            {
                return;
            }

            bool exported = _excelService.ExportPendingWorkflowRequestsByType(item.Request.Type, _viewModel.GetPendingRequestsByType(item.Request.Type));
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير الطلبات المعلقة من نوع {item.Request.TypeLabel}."
                    : $"تم إلغاء تصدير الطلبات المعلقة من نوع {item.Request.TypeLabel}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingExtensionRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Extension,
                _viewModel.GetPendingRequestsByType(RequestType.Extension));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات التمديد المعلقة."
                    : "تم إلغاء تصدير طلبات التمديد المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingReductionRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Reduction,
                _viewModel.GetPendingRequestsByType(RequestType.Reduction));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات التخفيض المعلقة."
                    : "تم إلغاء تصدير طلبات التخفيض المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingReleaseRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Release,
                _viewModel.GetPendingRequestsByType(RequestType.Release));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات الإفراج المعلقة."
                    : "تم إلغاء تصدير طلبات الإفراج المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingLiquidationRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Liquidation,
                _viewModel.GetPendingRequestsByType(RequestType.Liquidation));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات التسييل المعلقة."
                    : "تم إلغاء تصدير طلبات التسييل المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingVerificationRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Verification,
                _viewModel.GetPendingRequestsByType(RequestType.Verification));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات التحقق المعلقة."
                    : "تم إلغاء تصدير طلبات التحقق المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingReplacementRequests_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                RequestType.Replacement,
                _viewModel.GetPendingRequestsByType(RequestType.Replacement));
            GetShell()?.SetStatus(
                exported
                    ? "تم تصدير جميع طلبات الاستبدال المعلقة."
                    : "تم إلغاء تصدير طلبات الاستبدال المعلقة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void CopyUrgentGuaranteeNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedUrgentGuarantee?.GuaranteeNo, "رقم الضمان");
        }

        private void CopyUrgentGuaranteeSupplier_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedUrgentGuarantee?.Supplier, "اسم المورد");
        }

        private void CopyUrgentGuaranteeReferenceType_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedUrgentGuarantee;
            CopyToClipboard(guarantee?.ReferenceTypeLabel, "نوع المرجع");
        }

        private void CopyUrgentGuaranteeReferenceNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedUrgentGuarantee?.ReferenceNumber, "رقم المرجع");
        }

        private void CopyPendingRequestGuaranteeNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedPendingRequest?.GuaranteeNo, "رقم الضمان");
        }

        private void CopyPendingRequestSupplier_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedPendingRequest?.Supplier, "اسم المورد");
        }

        private void CopyToClipboard(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                GetShell()?.SetStatus($"لا توجد قيمة متاحة لنسخ {label}.", ShellStatusTone.Warning);
                return;
            }

            Clipboard.SetText(value);
            GetShell()?.SetStatus($"تم نسخ {label}.", ShellStatusTone.Success);
        }

        private static bool TryGetTemporalStatus(Guarantee guarantee, out GuaranteeTimeStatus temporalStatus)
        {
            if (guarantee.IsExpired)
            {
                temporalStatus = GuaranteeTimeStatus.Expired;
                return true;
            }

            if (guarantee.IsExpiringSoon)
            {
                temporalStatus = GuaranteeTimeStatus.ExpiringSoon;
                return true;
            }

            temporalStatus = GuaranteeTimeStatus.Active;
            return true;
        }
    }
}
