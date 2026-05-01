using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel
    {
        public void ExecuteGlobalSearch()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve(GlobalSearchText, CurrentWorkspaceKey);
            if (!plan.MatchedAlias && !plan.HasSearchText)
            {
                return;
            }

            _diagnostics.RecordEvent(
                "shell.search",
                "execute",
                new
                {
                    Query = GlobalSearchText,
                    plan.TargetWorkspaceKey,
                    plan.SearchText,
                    plan.MatchedAlias
                });

            switch (plan.TargetWorkspaceKey)
            {
                case ShellWorkspaceKeys.Dashboard:
                    ShowDashboardWorkspace(
                        plan.HasSearchText ? plan.SearchText : null,
                        plan.HasInitialScopeFilter ? plan.InitialScopeFilter : null);
                    break;
                case ShellWorkspaceKeys.Guarantees:
                    if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
                    {
                        return;
                    }

                    if (plan.HasSearchText)
                    {
                        SearchText = plan.SearchText;
                    }

                    ShowGuaranteesWorkspace();
                    break;
                case ShellWorkspaceKeys.Banks:
                    ShowBanksWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Reports:
                    ShowReportsWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Settings:
                    ShowSettingsWorkspace(plan.SearchText);
                    break;
                default:
                    if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
                    {
                        return;
                    }

                    if (plan.HasSearchText)
                    {
                        SearchText = plan.SearchText;
                    }

                    ShowGuaranteesWorkspace();
                    break;
            }
        }

        private void ShowDashboardWorkspace()
        {
            ShowDashboardWorkspace(null, null);
        }

        private void ShowDashboardWorkspace(string? initialSearchText)
        {
            ShowDashboardWorkspace(initialSearchText, null);
        }

        private void ShowDashboardWorkspace(string? initialSearchText, string? initialScopeFilter)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Dashboard))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Dashboard,
                _workspaceFactory.CreateDashboardWorkspace(
                    SearchText,
                    SelectedBank,
                    AllBanksLabel,
                    SelectedGuaranteeType,
                    AllTypesLabel,
                    SelectedTimeStatus.Value,
                    HasLastFile,
                    LastFileGuaranteeNo,
                    LastFileSummary,
                    OpenGuaranteeContextFromDashboard,
                    ShowGuaranteesWorkspace,
                    initialSearchText,
                    initialScopeFilter));
        }

        private void ShowGuaranteesWorkspace()
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            ActivateWorkspace(ShellWorkspaceKeys.Guarantees, null);
        }

        private void ShowGuaranteesForBank(string? bank)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            string selectedBank = string.IsNullOrWhiteSpace(bank) ? AllBanksLabel : bank.Trim();
            ActivateWorkspace(ShellWorkspaceKeys.Guarantees, null);
            SetGuaranteeFilters(
                string.Empty,
                selectedBank,
                AllTypesLabel,
                FilterOption.AllTimeStatuses,
                GuaranteeStatusFilter.Active);
            _shellStatus.ShowInfo("تم عرض ضمانات البنك.", $"الضمانات • {selectedBank}");
        }

        private void ResumeLastFile()
        {
            if (!HasLastFile)
            {
                return;
            }

            Guarantee? guarantee = _sessionCoordinator.ResolveLastFileGuarantee(_lastFileState, _database);
            if (guarantee == null)
            {
                SetLastFileState(ShellLastFileState.Empty);
                return;
            }

            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            CloseActiveWorkspace();

            SetGuaranteeFilters(
                string.Empty,
                AllBanksLabel,
                AllTypesLabel,
                FilterOption.AllTimeStatuses,
                ResolveGuaranteeStatusFilter(guarantee));

            GuaranteeRow? row = Guarantees.FirstOrDefault(item => item.RootId == _lastFileState.RootId)
                ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);
            if (row != null)
            {
                SelectedGuarantee = row;
                return;
            }

            RefreshAfterWorkflowChange(_lastFileState.RootId);
            _diagnostics.RecordEvent(
                "shell.session",
                "resume-last-file",
                new
                {
                    _lastFileState.RootId,
                    _lastFileState.GuaranteeNo
                });
        }

        private void CloseActiveWorkspace()
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            ActivateWorkspace(ShellWorkspaceKeys.Guarantees, null);
        }

        private void ShowBanksWorkspace()
        {
            ShowBanksWorkspace(null);
        }

        private void ShowBanksWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Banks))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Banks,
                _workspaceFactory.CreateBanksWorkspace(ShowGuaranteesForBank, initialSearchText));
        }

        private void ShowReportsWorkspace()
        {
            ShowReportsWorkspace(null);
        }

        private void ShowReportsWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Reports))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Reports,
                _workspaceFactory.CreateReportsWorkspace(initialSearchText));
        }

        private void ShowSettingsWorkspace()
        {
            ShowSettingsWorkspace(null);
        }

        private void ShowSettingsWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Settings))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Settings,
                _workspaceFactory.CreateSettingsWorkspace(RefreshAfterDataReset, initialSearchText));
        }

        private void RequestExit()
        {
            if (!CanNavigateAway("إغلاق البرنامج"))
            {
                return;
            }

            Application.Current.MainWindow?.Close();
        }

        private bool CanNavigateToWorkspace(string targetWorkspaceKey)
        {
            if (string.Equals(CurrentWorkspaceKey, targetWorkspaceKey, StringComparison.Ordinal))
            {
                return true;
            }

            return CanNavigateAway("التنقل بين المساحات");
        }

        private bool CanNavigateAway(string actionLabel)
        {
            if (_navigationGuard.CanNavigateAway(out string blockingReason))
            {
                return true;
            }

            string message = string.IsNullOrWhiteSpace(blockingReason)
                ? $"تعذر {actionLabel} قبل إكمال العملية الحالية."
                : blockingReason;
            MessageBox.Show(message, "حراسة التنقل", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void ActivateWorkspace(string key, FrameworkElement? content)
        {
            CurrentWorkspaceKey = key;
            OnPropertyChanged(nameof(CurrentWorkspaceDisplayTitle));
            ActiveWorkspaceContent = content;
            _diagnostics.RecordEvent(
                "shell.navigation",
                "workspace-activated",
                new
                {
                    WorkspaceKey = key,
                    ContentType = content?.GetType().Name ?? nameof(GuaranteesDashboardView)
                });
            WriteDiagnosticsState("workspace-activated");
        }
    }
}
