using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class ShellWorkspaceFactory
    {
        private readonly IDatabaseService _database;
        private readonly IWorkflowService _workflow;
        private readonly GuaranteeWorkspaceDataService _guaranteeData;
        private readonly ReportsWorkspaceCoordinator _reportsWorkspace;

        public ShellWorkspaceFactory(
            IDatabaseService database,
            IWorkflowService workflow,
            GuaranteeWorkspaceDataService guaranteeData,
            ReportsWorkspaceCoordinator reportsWorkspace)
        {
            _database = database;
            _workflow = workflow;
            _guaranteeData = guaranteeData;
            _reportsWorkspace = reportsWorkspace;
        }

        public FrameworkElement CreateDashboardWorkspace(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeTimeStatus? selectedTimeStatus,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            string lastFileSummary,
            Action resumeLastFile,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
            Action showGuarantees,
            Action<string?, string?> showToday,
            Action<string?, int?> showRequests,
            Action<string?> showReports,
            string? initialSearchText = null,
            string? initialScopeFilter = null)
        {
            return new DashboardWorkspaceSurface(
                () => _guaranteeData.QueryGuarantees(
                    searchText,
                    selectedBank,
                    allBanksLabel,
                    selectedGuaranteeType,
                    allTypesLabel,
                    selectedTimeStatus,
                    includeAttachments: true,
                    limit: null),
                () => _database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestStatus = RequestStatus.Pending,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending,
                    Limit = 200
                }),
                hasLastFile,
                lastFileGuaranteeNo,
                lastFileSummary,
                resumeLastFile,
                openGuaranteeContext,
                showGuarantees,
                showToday,
                showRequests,
                showReports,
                showGuarantees,
                initialSearchText,
                initialScopeFilter);
        }

        public FrameworkElement CreateRequestsWorkspace(
            Action<int> refreshAfterWorkflowChange,
            Action closeRequested,
            Action? selectionChanged = null,
            string? initialSearchText = null,
            int? initialRequestId = null)
        {
            WorkflowRequestQueryOptions options = new()
            {
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending,
                Limit = 160
            };

            return new RequestsWorkspaceSurface(
                () => _database.QueryWorkflowRequests(options),
                _database,
                _workflow,
                App.CurrentApp.GetRequiredService<IExcelService>(),
                refreshAfterWorkflowChange,
                closeRequested,
                selectionChanged,
                initialSearchText,
                initialRequestId);
        }

        public FrameworkElement CreateBanksWorkspace(Action closeRequested, string? initialSearchText = null)
        {
            List<Guarantee> guarantees = _database.QueryGuarantees(new GuaranteeQueryOptions());
            return new BanksWorkspaceSurface(guarantees, closeRequested, initialSearchText);
        }

        public FrameworkElement CreateReportsWorkspace(Action closeRequested, string? initialSearchText = null)
        {
            return new ReportsWorkspaceSurface(
                WorkspaceReportCatalog.PortfolioActions.Concat(WorkspaceReportCatalog.OperationalActions).ToList(),
                _reportsWorkspace,
                closeRequested,
                initialSearchText);
        }

        public FrameworkElement CreateSettingsWorkspace(Action closeRequested, Action refreshAfterDataReset, string? initialSearchText = null)
        {
            return new SettingsWorkspaceSurface(closeRequested, refreshAfterDataReset, initialSearchText);
        }
    }
}
