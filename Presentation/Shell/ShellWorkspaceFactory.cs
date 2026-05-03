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
        private const int DashboardGuaranteeLoadLimit = 500;
        private const int DashboardPendingRequestLoadLimit = 500;

        private readonly IDatabaseService _database;
        private readonly GuaranteeWorkspaceDataService _guaranteeData;
        private readonly ReportsWorkspaceCoordinator _reportsWorkspace;

        public ShellWorkspaceFactory(
            IDatabaseService database,
            GuaranteeWorkspaceDataService guaranteeData,
            ReportsWorkspaceCoordinator reportsWorkspace)
        {
            _database = database;
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
            Action<int, GuaranteeFocusArea, int?> openGuaranteeContext,
            Action showGuarantees,
            string? initialSearchText = null,
            string? initialScopeFilter = null)
        {
            List<WorkflowRequestListItem>? pendingRequestsCache = null;
            IReadOnlyList<WorkflowRequestListItem> LoadPendingRequests()
            {
                pendingRequestsCache ??= _database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestStatus = RequestStatus.Pending,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending,
                    Limit = DashboardPendingRequestLoadLimit
                });

                return pendingRequestsCache;
            }

            IReadOnlyList<Guarantee> LoadDashboardGuarantees()
            {
                IReadOnlyList<int> pendingRootIds = LoadPendingRequests()
                    .Select(request => request.RootGuaranteeId)
                    .Distinct()
                    .ToList();

                return _database.QueryGuarantees(new GuaranteeQueryOptions
                {
                    SearchText = searchText,
                    Bank = selectedBank == allBanksLabel ? null : selectedBank,
                    GuaranteeType = selectedGuaranteeType == allTypesLabel ? null : selectedGuaranteeType,
                    TimeStatus = selectedTimeStatus,
                    NeedsExpiryFollowUpOnly = !selectedTimeStatus.HasValue,
                    FollowUpPendingRootIds = pendingRootIds,
                    IncludeAttachments = false,
                    Limit = DashboardGuaranteeLoadLimit,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                });
            }

            return new DashboardWorkspaceSurface(
                LoadDashboardGuarantees,
                LoadPendingRequests,
                hasLastFile,
                lastFileGuaranteeNo,
                lastFileSummary,
                openGuaranteeContext,
                showGuarantees,
                initialSearchText,
                initialScopeFilter);
        }

        public FrameworkElement CreateBanksWorkspace(Action<string?> showGuaranteesForBank, string? initialSearchText = null)
        {
            List<BankPortfolioSummary> summaries = _database.GetBankPortfolioSummaries();
            return new BanksWorkspaceSurface(
                summaries,
                _database.GetBankReferences(),
                showGuaranteesForBank,
                _database.AddBankReference,
                initialSearchText);
        }

        public FrameworkElement CreateReportsWorkspace(string? initialSearchText = null)
        {
            return new ReportsWorkspaceSurface(
                WorkspaceReportCatalog.PortfolioActions.Concat(WorkspaceReportCatalog.OperationalActions).ToList(),
                _reportsWorkspace,
                initialSearchText);
        }

        public FrameworkElement CreateSettingsWorkspace(Action refreshAfterDataReset, string? initialSearchText = null)
        {
            return new SettingsWorkspaceSurface(refreshAfterDataReset, initialSearchText);
        }
    }
}
