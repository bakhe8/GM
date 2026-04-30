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
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
                }),
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
            List<Guarantee> guarantees = _database.QueryGuarantees(new GuaranteeQueryOptions());
            return new BanksWorkspaceSurface(
                guarantees,
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
