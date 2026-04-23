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
    public partial class OperationCenterView : UserControl, IOperationCenterWorkspace
    {
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly IContextActionService _contextActionService;
        private readonly OperationCenterViewModel _viewModel;
        private readonly SecondaryWindowManager _windowManager = SecondaryWindowManager.Instance;
        private bool _suppressSelectionSideSheet;
        private int? _requestIdToFocus;

        public OperationCenterView(
            IDatabaseService databaseService,
            IWorkflowService workflowService,
            IExcelService excelService,
            IContextActionService contextActionService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _workflowService = workflowService;
            _excelService = excelService;
            _contextActionService = contextActionService;
            _viewModel = new OperationCenterViewModel(workflowService, databaseService);
            DataContext = _viewModel;
            Loaded += (_, _) => RefreshView();
            IsVisibleChanged += OperationCenterView_IsVisibleChanged;
        }

        private void OperationCenterView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GridRequests.UpdateLayout();
                    foreach (var column in GridRequests.Columns)
                    {
                        var originalWidth = column.Width;
                        column.Width = 0;
                        column.Width = originalWidth;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        public void SetRequestFocus(int? requestId)
        {
            _requestIdToFocus = requestId;
            _viewModel.SetRequestFocus(requestId);
        }

        public void RefreshView()
        {
            int? requestIdToReveal = _requestIdToFocus;
            bool shouldRevealFocusedRequest = requestIdToReveal.HasValue;
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.SetRequestFocus(requestIdToReveal);
                _viewModel.Refresh();
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            UpdateCreateRequestPanel();

            if (shouldRevealFocusedRequest && SelectedItem?.Request.Id == requestIdToReveal)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WorkflowRequestListItem? item = SelectedItem;
                    if (item == null || item.Request.Id != requestIdToReveal)
                    {
                        _requestIdToFocus = null;
                        GetShell()?.CloseSideSheet();
                        return;
                    }

                    GridRequests.SelectedItem = item;
                    GridRequests.ScrollIntoView(item);
                    UpdateSelectionState(item);
                    _requestIdToFocus = null;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            _requestIdToFocus = null;
            GetShell()?.CloseSideSheet();
        }

        public void ApplyShellSearch(string query)
        {
            _requestIdToFocus = null;
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ApplyShellSearch(query);
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private WorkflowRequestListItem? SelectedItem => _viewModel.SelectedItem;

        private void ApplyFilters()
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ApplyFilters(
                    TxtRequestSearch.Text,
                    CmbTypeFilter.SelectedItem?.ToString(),
                    CmbStatusFilter.SelectedItem?.ToString());
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private void UpdateSelectionState(WorkflowRequestListItem? item)
        {
            MainWindow? shell = GetShell();
            if (shell == null)
            {
                return;
            }

            if (item == null)
            {
                shell.CloseSideSheet();
                return;
            }

            shell.ShowSideSheet(
                "معاينة الطلب",
                "قراءة سريعة لحالة الطلب والوثائق المرتبطة.",
                new RequestSideSheetView(item, _databaseService, _workflowService));
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ApplyFilters();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ClearFilters();
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private void GridRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSideSheet)
            {
                return;
            }

            UpdateSelectionState(SelectedItem);
        }

        private void GridRequests_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataGridContextMenuAssist.TryGetRowItem<WorkflowRequestListItem>(e, out WorkflowRequestListItem? item) &&
                item != null &&
                SelectedItem?.Request.Id == item.Request.Id)
            {
                UpdateSelectionState(item);
            }
        }

        private void GridRequests_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressSelectionSideSheet = true;
            try
            {
                if (!DataGridContextMenuAssist.TrySelectRowFromRightClick(GridRequests, e) || SelectedItem == null)
                {
                    return;
                }
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            ContextMenu contextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildRequestSections(_contextActionService),
                ResolveRequestContextActionHandler,
                ResolveRequestContextActionAvailability);

            DataGridContextMenuAssist.OpenContextMenuAtPointer(GridRequests, contextMenu);
            e.Handled = true;
        }

        private void GridRequests_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SelectedItem == null || !DataGridContextMenuAssist.IsPointerOverRow(GridRequests))
            {
                GridRequests.ContextMenu = null;
                e.Handled = true;
                return;
            }

            GridRequests.ContextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildRequestSections(_contextActionService),
                ResolveRequestContextActionHandler,
                ResolveRequestContextActionAvailability);
        }

        private void OpenGuarantee_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                GetShell()?.SetStatus("حدد طلبًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee? guarantee = _viewModel.GetCurrentGuaranteeForItem(SelectedItem);
            if (guarantee == null)
            {
                GetShell()?.SetStatus("تعذر العثور على ملف الضمان المرتبط.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(guarantee, "شاشة الطلبات", true, requestIdToFocus: SelectedItem.Request.Id);
        }

        private void OpenLetter_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem?.Request.HasLetter != true)
            {
                GetShell()?.SetStatus("لا يوجد خطاب طلب محفوظ لهذا السجل.", ShellStatusTone.Warning);
                return;
            }

            try
            {
                _workflowService.OpenRequestLetter(SelectedItem.Request);
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
            if (SelectedItem == null)
            {
                GetShell()?.SetStatus("حدد طلبًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            if (SelectedItem.Request.HasResponseDocument)
            {
                try
                {
                    _workflowService.OpenResponseDocument(SelectedItem.Request);
                    GetShell()?.SetStatus("تم فتح مستند الرد.", ShellStatusTone.Info);
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(ex, "تعذر فتح مستند الرد.");
                    GetShell()?.SetStatus("فشل فتح مستند الرد.", ShellStatusTone.Error);
                }

                return;
            }

            if (SelectedItem.Request.Status == RequestStatus.Pending)
            {
                RecordResponse_Click(sender, e);
                return;
            }

            if (!_viewModel.CanAttachResponseDocument)
            {
                GetShell()?.SetStatus("تعذر استخدام إجراء مستند الرد لهذا الطلب.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            AttachWorkflowResponseDocumentWindow dialog = new(SelectedItem);
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
                    SelectedItem.Request.Id,
                    dialog.ResponseDocumentPath,
                    dialog.AdditionalNotes);

                RefreshView();
                shell?.SetStatus("تم إلحاق مستند رد البنك بهذا الطلب.", ShellStatusTone.Success);
            }
            catch (DeferredFilePromotionException ex)
            {
                RefreshView();
                AppDialogService.ShowWarning(ex.UserMessage);
                shell?.SetStatus("تم تحديث الطلب، لكن بعض الملفات ما زالت بانتظار الاستكمال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر إلحاق مستند الرد.");
                shell?.SetStatus("فشل إلحاق مستند الرد.", ShellStatusTone.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
            GetShell()?.SetStatus("تم تحديث قائمة الطلبات.", ShellStatusTone.Success);
        }

        private void GridRequests_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenGuarantee_Click(sender, new RoutedEventArgs());
        }

        private void UpdateCreateRequestPanel()
        {
            ContextActionPanelFactory.Populate(
                CreateRequestActionPanel,
                BuildCreateRequestSections(),
                ResolveCreateRequestActionHandler,
                true,
                ResolveCreateRequestActionAvailability,
                "execute.create-extension",
                "execute.create-reduction");
        }

        private IReadOnlyList<ContextActionSection> BuildCreateRequestSections()
        {
            IReadOnlyList<ContextActionSection> sourceSections = _contextActionService.GetGuaranteeActions();
            ContextActionDefinition[] actions = ContextActionMenuFactory.FindActionsByIds(
                sourceSections,
                "execute.create-extension",
                "execute.create-reduction",
                "execute.create-release",
                "execute.create-liquidation",
                "execute.create-verification",
                "execute.create-replacement",
                "execute.create-annulment").ToArray();

            return new[]
            {
                new ContextActionSection(
                    "إنشاء من الضمانات المؤهلة",
                    "يفتح لكل نوع نافذة الطلب نفسها، لكن مع قائمة الضمانات المؤهلة بدل الاعتماد على صف محدد مسبقًا.",
                    actions)
            };
        }

        private RoutedEventHandler? ResolveCreateRequestActionHandler(string actionId)
        {
            return actionId switch
            {
                "execute.create-extension" => CreateExtensionFromEligibleGuarantees_Click,
                "execute.create-reduction" => CreateReductionFromEligibleGuarantees_Click,
                "execute.create-release" => CreateReleaseFromEligibleGuarantees_Click,
                "execute.create-liquidation" => CreateLiquidationFromEligibleGuarantees_Click,
                "execute.create-verification" => CreateVerificationFromEligibleGuarantees_Click,
                "execute.create-replacement" => CreateReplacementFromEligibleGuarantees_Click,
                "execute.create-annulment" => CreateAnnulmentFromEligibleGuarantees_Click,
                _ => null
            };
        }

        private ContextActionAvailability ResolveCreateRequestActionAvailability(ContextActionDefinition action)
        {
            return _viewModel.GetRequestCreationAvailability(action.Id);
        }

        private RoutedEventHandler? ResolveRequestContextActionHandler(string actionId)
        {
            return actionId switch
            {
                "request.record-response" => RecordResponse_Click,
                "request.open-current-guarantee" => OpenGuarantee_Click,
                "request.open-history" => OpenGuaranteeHistory_Click,
                "request.open-letter" => OpenLetter_Click,
                "request.open-response" => OpenResponse_Click,
                "request.export-pending-same-type" => ExportPendingSameType_Click,
                "request.export-pending-extension" => ExportPendingExtensions_Click,
                "request.export-pending-reduction" => ExportPendingReductions_Click,
                "request.export-pending-release" => ExportPendingReleases_Click,
                "request.export-pending-liquidation" => ExportPendingLiquidations_Click,
                "request.export-pending-verification" => ExportPendingVerifications_Click,
                "request.export-pending-replacement" => ExportPendingReplacements_Click,
                "workspace.request.copy-guarantee-no" => CopyRequestGuaranteeNumber_Click,
                "workspace.request.copy-supplier" => CopyRequestSupplier_Click,
                _ => null
            };
        }

        private ContextActionAvailability ResolveRequestContextActionAvailability(ContextActionDefinition action)
        {
            WorkflowRequestListItem? item = SelectedItem;
            if (item == null)
            {
                return ContextActionAvailability.Disabled("حدد طلبًا أولًا.");
            }

            return _viewModel.GetContextActionAvailability(action.Id, item);
        }

        private void RecordResponse_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedItem;
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

        private void CreateExtensionFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForExtension();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة مؤهلة الآن لطلب التمديد.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateExtensionRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateExtensionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedExpiryDate,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب التمديد للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب التمديد.",
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

        private void CreateReductionFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForReduction();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة ذات مبلغ قابل للتخفيض الآن.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReductionRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateReductionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedAmount,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب التخفيض للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب التخفيض.",
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

        private void CreateReleaseFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForRelease();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة مؤهلة الآن لطلب الإفراج.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReleaseRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateReleaseRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب الإفراج للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب الإفراج.",
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

        private void CreateLiquidationFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForLiquidation();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة مؤهلة الآن لطلب التسييل.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateLiquidationRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateLiquidationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب التسييل للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب التسييل.",
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

        private void CreateVerificationFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForVerification();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة مؤهلة الآن لطلب التحقق.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateVerificationRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateVerificationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب التحقق للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب التحقق.",
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

        private void CreateReplacementFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForReplacement();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات نشطة مؤهلة الآن لطلب الاستبدال.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateReplacementRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateReplacementRequest(
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

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب الاستبدال للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب الاستبدال.",
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

        private void CreateAnnulmentFromEligibleGuarantees_Click(object sender, RoutedEventArgs e)
        {
            List<Guarantee> guarantees = _workflowService.GetGuaranteesEligibleForAnnulment()
                .Where(item => !_databaseService.HasPendingWorkflowRequest(item.RootId ?? item.Id, RequestType.Annulment))
                .ToList();
            MainWindow? shell = GetShell();
            if (guarantees.Count == 0)
            {
                shell?.SetStatus("لا توجد ضمانات مفرج عنها أو مسيّلة متاحة الآن لطلب النقض.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateAnnulmentRequestWindow(guarantees);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Guarantee? guarantee = guarantees.FirstOrDefault(item => item.Id == dialog.SelectedGuaranteeId);
            try
            {
                WorkflowRequest request = _workflowService.CreateAnnulmentRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestReason,
                    dialog.CreatedBy);

                _requestIdToFocus = request.Id;
                RefreshView();
                shell?.SetStatus(
                    guarantee != null
                        ? $"تم تسجيل طلب النقض للضمان رقم {guarantee.GuaranteeNo}."
                        : "تم تسجيل طلب النقض.",
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

        private void OpenGuaranteeHistory_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedItem;
            if (item == null)
            {
                return;
            }

            Guarantee? guarantee = _viewModel.GetCurrentGuaranteeForItem(item);
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

        private void ExportPendingSameType_Click(object sender, RoutedEventArgs e)
        {
            WorkflowRequestListItem? item = SelectedItem;
            if (item == null)
            {
                return;
            }

            bool exported = _excelService.ExportPendingWorkflowRequestsByType(
                item.Request.Type,
                _viewModel.GetPendingRequestsByType(item.Request.Type));
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير الطلبات المعلقة من نوع {item.Request.TypeLabel}."
                    : $"تم إلغاء تصدير الطلبات المعلقة من نوع {item.Request.TypeLabel}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportPendingExtensions_Click(object sender, RoutedEventArgs e)
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

        private void ExportPendingReductions_Click(object sender, RoutedEventArgs e)
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

        private void ExportPendingReleases_Click(object sender, RoutedEventArgs e)
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

        private void ExportPendingLiquidations_Click(object sender, RoutedEventArgs e)
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

        private void ExportPendingVerifications_Click(object sender, RoutedEventArgs e)
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

        private void ExportPendingReplacements_Click(object sender, RoutedEventArgs e)
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

        private void CopyRequestGuaranteeNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedItem?.GuaranteeNo, "رقم الضمان");
        }

        private void CopyRequestSupplier_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedItem?.Supplier, "اسم المورد");
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
    }
}
