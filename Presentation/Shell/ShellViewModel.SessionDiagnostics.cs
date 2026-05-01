using System;
using System.Collections.Generic;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel
    {
        private void RememberLastFile(GuaranteeRow row)
        {
            SetLastFileState(_sessionCoordinator.RememberLastFile(row));
        }

        private void SetLastFileState(ShellLastFileState state)
        {
            if (EqualityComparer<ShellLastFileState>.Default.Equals(_lastFileState, state))
            {
                return;
            }

            _lastFileState = state;
            OnPropertyChanged(nameof(HasLastFile));
            OnPropertyChanged(nameof(LastFileGuaranteeNo));
            OnPropertyChanged(nameof(LastFileSummary));
            if (ResumeLastFileCommand is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }

            _diagnostics.RecordEvent(
                "shell.session",
                "last-file-updated",
                new
                {
                    state.HasLastFile,
                    state.RootId,
                    state.GuaranteeNo,
                    state.Summary
                });
            WriteDiagnosticsState("last-file");
        }

        private void WriteDiagnosticsState(string reason)
        {
            ShellDiagnosticsSelection diagnosticsSelection = ResolveDiagnosticsSelection();
            _diagnostics.UpdateShellState(new UiShellDiagnosticsState(
                DateTimeOffset.Now,
                reason,
                CurrentWorkspaceKey,
                ActiveWorkspaceContent?.GetType().Name ?? nameof(GuaranteesDashboardView),
                GlobalSearchText,
                SearchText,
                SelectedBank,
                SelectedGuaranteeType,
                SelectedGuaranteeStatusFilter.ToString(),
                Guarantees.Count,
                FooterSummary,
                PendingRequestCount,
                ExpiredCount,
                ExpiredFollowUpCount,
                ExpiringSoonCount,
                ActiveCount,
                HasLastFile,
                LastFileGuaranteeNo,
                LastFileSummary,
                diagnosticsSelection.GuaranteeId,
                diagnosticsSelection.RootGuaranteeId,
                diagnosticsSelection.GuaranteeNo,
                diagnosticsSelection.Supplier,
                diagnosticsSelection.Bank,
                SelectedOperationalInquiryOption?.Id ?? string.Empty,
                HasLatestInquiryResult,
                HasLatestInquirySuggestedSection,
                LatestInquirySuggestedSectionLabel));
        }

        private ShellDiagnosticsSelection ResolveDiagnosticsSelection()
        {
            return new ShellDiagnosticsSelection(
                SelectedGuarantee?.Id,
                SelectedGuarantee?.RootId,
                SelectedGuarantee?.GuaranteeNo ?? string.Empty,
                SelectedGuarantee?.Supplier ?? string.Empty,
                SelectedGuarantee?.Bank ?? string.Empty);
        }

        private sealed record ShellDiagnosticsSelection(
            int? GuaranteeId,
            int? RootGuaranteeId,
            string GuaranteeNo,
            string Supplier,
            string Bank);
    }
}
