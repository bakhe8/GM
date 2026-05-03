using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel : INotifyPropertyChanged
    {
        private ShellViewModel(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            INavigationGuard navigationGuard,
            IShellStatusService shellStatus,
            IUiDiagnosticsService diagnostics)
        {
            _database = database;
            _navigationGuard = navigationGuard;
            _shellStatus = shellStatus;
            _diagnostics = diagnostics;
            _guaranteeData = new GuaranteeWorkspaceDataService(_database);
            _guaranteeWorkspace = new GuaranteeWorkspaceCoordinator(
                _database,
                workflow,
                shellStatus,
                LoadFilterOptions,
                RefreshAfterWorkflowChange);
            _sessionCoordinator = new ShellSessionCoordinator();
            _workspaceFactory = new ShellWorkspaceFactory(_database, _guaranteeData, new ReportsWorkspaceCoordinator(_database, excel));
            _shellStatus.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ShellStatusPrimaryText));
                OnPropertyChanged(nameof(ShellStatusSecondaryText));
                OnPropertyChanged(nameof(ShellStatusPrimaryBrush));
            };
            CreateNewGuaranteeCommand = new RelayCommand(_ => CreateNewGuarantee());
            EditGuaranteeCommand = new RelayCommand(parameter => EditGuarantee(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            PreviousGuaranteePageCommand = new RelayCommand(_ => MoveGuaranteePage(-1), _ => CanGoToPreviousGuaranteePage);
            NextGuaranteePageCommand = new RelayCommand(_ => MoveGuaranteePage(1), _ => CanGoToNextGuaranteePage);
            GoToGuaranteePageCommand = new RelayCommand(GoToGuaranteePage, CanGoToGuaranteePage);
            SelectGuaranteeCommand = new RelayCommand(parameter => SelectGuarantee(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ApplyGuaranteeStatusFilterCommand = new RelayCommand(ApplyGuaranteeStatusFilter);
            CreateExtensionRequestCommand = new RelayCommand(parameter => CreateExtensionRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReleaseRequestCommand = new RelayCommand(parameter => CreateReleaseRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReductionRequestCommand = new RelayCommand(parameter => CreateReductionRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateLiquidationRequestCommand = new RelayCommand(parameter => CreateLiquidationRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateVerificationRequestCommand = new RelayCommand(parameter => CreateVerificationRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReplacementRequestCommand = new RelayCommand(parameter => CreateReplacementRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            RegisterBankResponseCommand = new RelayCommand(parameter => RegisterBankResponse(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CopyGuaranteeNoCommand = new RelayCommand(parameter => CopyGuaranteeNo(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeSupplierCommand = new RelayCommand(parameter => CopyGuaranteeSupplier(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeReferenceTypeCommand = new RelayCommand(parameter => CopyGuaranteeReferenceType(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeReferenceNumberCommand = new RelayCommand(parameter => CopyGuaranteeReferenceNumber(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeTypeCommand = new RelayCommand(parameter => CopyGuaranteeType(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeIssueDateCommand = new RelayCommand(parameter => CopyGuaranteeIssueDate(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeExpiryDateCommand = new RelayCommand(parameter => CopyGuaranteeExpiryDate(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            OpenAttachmentCommand = new RelayCommand(parameter => OpenAttachment(parameter as AttachmentItem), parameter => parameter is AttachmentItem);
            OpenTimelineEvidenceCommand = new RelayCommand(parameter => OpenTimelineEvidence(parameter as TimelineItem), parameter => parameter is TimelineItem item && item.HasEvidenceAction);
            OpenOutputLetterCommand = new RelayCommand(parameter => OpenOutputLetter(parameter as GuaranteeOutputPreviewItem), parameter => parameter is GuaranteeOutputPreviewItem item && item.CanOpenLetter);
            OpenOutputResponseCommand = new RelayCommand(parameter => OpenOutputResponse(parameter as GuaranteeOutputPreviewItem), parameter => parameter is GuaranteeOutputPreviewItem item && item.CanOpenResponse);
            ShowAllAttachmentsCommand = new RelayCommand(_ => ShowAllAttachments(), _ => SelectedGuarantee != null);
            ResumeLastFileCommand = new RelayCommand(_ => ResumeLastFile(), _ => HasLastFile);
            ShowDashboardCommand = new RelayCommand(_ => ShowDashboardWorkspace());
            ShowGuaranteesCommand = new RelayCommand(_ => ShowGuaranteesWorkspace());
            ShowBanksCommand = new RelayCommand(_ => ShowBanksWorkspace());
            ShowReportsCommand = new RelayCommand(_ => ShowReportsWorkspace());
            ShowSettingsCommand = new RelayCommand(_ => ShowSettingsWorkspace());
            ExecuteGlobalSearchCommand = new RelayCommand(_ => ExecuteGlobalSearch());
            ExitCommand = new RelayCommand(_ => RequestExit());
        }

        public static ShellViewModel Create(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            INavigationGuard navigationGuard,
            IShellStatusService shellStatus,
            IUiDiagnosticsService diagnostics)
        {
            var viewModel = new ShellViewModel(database, workflow, excel, navigationGuard, shellStatus, diagnostics);
            viewModel.LoadFilterOptions();
            viewModel.Refresh();
            viewModel.ShowDashboardWorkspace();
            viewModel.WriteDiagnosticsState("startup");
            return viewModel;
        }

        private int? ResolveContextRequestId(GuaranteeRow target)
        {
            return _database.GetWorkflowRequestsByRootId(target.RootId)
                .OrderBy(request => request.Status == RequestStatus.Pending ? 0 : 1)
                .ThenByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .FirstOrDefault()
                ?.Id;
        }

        private void OpenGuaranteeContextFromDashboard(int rootId, GuaranteeFocusArea area, int? requestIdToFocus)
        {
            OpenGuaranteeContext("dashboard", rootId, area, requestIdToFocus);
        }

        private void OpenGuaranteeContext(string sourceKey, int rootId, GuaranteeFocusArea area, int? requestIdToFocus)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            Guarantee? guarantee = _database.GetCurrentGuaranteeByRootId(rootId);
            SetGuaranteeFilters(
                string.Empty,
                AllBanksLabel,
                AllTypesLabel,
                FilterOption.AllTimeStatuses,
                guarantee == null ? GuaranteeStatusFilter.Active : ResolveGuaranteeStatusFilter(guarantee));
            if (guarantee == null)
            {
                ShowGuaranteesWorkspace();
                return;
            }

            GuaranteeRow? row = Guarantees.FirstOrDefault(item => item.RootId == rootId)
                                ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);

            if (row == null)
            {
                RefreshAfterWorkflowChange(rootId);
                row = Guarantees.FirstOrDefault(item => item.RootId == rootId)
                      ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);
            }

            if (row == null)
            {
                ShowGuaranteesWorkspace();
                return;
            }

            RouteGuaranteeContext(row, area, requestIdToFocus, sourceKey);
        }

        public void RouteGuaranteeContext(
            GuaranteeRow row,
            GuaranteeFocusArea area,
            int? requestIdToFocus,
            string sourceKey = "guarantee")
        {
            SelectedGuarantee = row;
            GuaranteeFocusArea resolvedArea = area == GuaranteeFocusArea.None
                ? GuaranteeFocusArea.Series
                : area;

            GuaranteeFocusArea targetArea = ResolveTimelineTargetArea(resolvedArea, requestIdToFocus);
            int? targetRequestId = targetArea == GuaranteeFocusArea.Requests
                ? requestIdToFocus ?? ResolveContextRequestId(row)
                : null;

            ShowGuaranteesWorkspace();
            SelectedGuarantee = row;
            FocusGuaranteeSection(targetArea, targetRequestId);

            _diagnostics.RecordEvent(
                $"{sourceKey}.action",
                "route-guarantee-context",
                new
                {
                    row.RootId,
                    row.GuaranteeNo,
                    FocusArea = resolvedArea.ToString(),
                    requestIdToFocus
                });
            WriteDiagnosticsState($"{sourceKey}-route-guarantee-context");
        }

        private static GuaranteeFocusArea ResolveTimelineTargetArea(
            GuaranteeFocusArea requestedArea,
            int? requestIdToFocus)
        {
            if (requestIdToFocus.HasValue || requestedArea == GuaranteeFocusArea.Requests)
            {
                return GuaranteeFocusArea.Requests;
            }

            if (requestedArea == GuaranteeFocusArea.Outputs)
            {
                return GuaranteeFocusArea.Outputs;
            }

            return requestedArea == GuaranteeFocusArea.None
                ? GuaranteeFocusArea.Series
                : requestedArea;
        }

private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
