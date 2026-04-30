using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel
    {
        public void Refresh()
        {
            GuaranteeRow? previousSelection = SelectedGuarantee;
            GuaranteeWorkspaceSnapshot snapshot = _guaranteeData.BuildSnapshot(
                SearchText,
                SelectedBank,
                AllBanksLabel,
                SelectedGuaranteeType,
                AllTypesLabel,
                SelectedTimeStatus.Value,
                PageSize,
                CurrentGuaranteePage);

            Guarantees.Clear();
            foreach (GuaranteeRow row in snapshot.Rows)
            {
                Guarantees.Add(row);
            }

            PendingRequestCount = snapshot.PendingRequestCount;
            PendingRequestMeta = snapshot.PendingRequestMeta;
            ExpiredCount = snapshot.ExpiredCount;
            ExpiredMeta = snapshot.ExpiredMeta;
            ExpiredFollowUpCount = snapshot.ExpiredFollowUpCount;
            ExpiredFollowUpMeta = snapshot.ExpiredFollowUpMeta;
            ExpiringSoonCount = snapshot.ExpiringSoonCount;
            ExpiringSoonMeta = snapshot.ExpiringSoonMeta;
            ActiveCount = snapshot.ActiveCount;
            ActiveMeta = snapshot.ActiveMeta;
            TotalGuaranteePages = snapshot.TotalPages;
            CurrentGuaranteePage = snapshot.CurrentPage;
            RefreshGuaranteePagerButtons();
            FooterSummary = snapshot.FooterSummary;

            _trackSelectedGuaranteeAsLastFile = false;
            SelectedGuarantee = ResolvePreferredVisibleGuarantee(previousSelection);
            _trackSelectedGuaranteeAsLastFile = true;

            WriteDiagnosticsState("refresh");
        }

        private GuaranteeRow? ResolvePreferredVisibleGuarantee(GuaranteeRow? previousSelection)
        {
            if (previousSelection != null)
            {
                int previousRootId = previousSelection.RootId > 0
                    ? previousSelection.RootId
                    : previousSelection.Id;

                GuaranteeRow? matchingRow = Guarantees.FirstOrDefault(row =>
                    row.RootId == previousRootId
                    || row.Id == previousSelection.Id);

                if (matchingRow != null)
                {
                    return matchingRow;
                }
            }

            return Guarantees.FirstOrDefault();
        }

        private void SynchronizeSelectedTableGuarantee(GuaranteeRow? selected)
        {
            GuaranteeRow? tableRow = selected == null
                ? null
                : selected.IsCurrentVersion
                    ? Guarantees.FirstOrDefault(row => ReferenceEquals(row, selected) || row.Id == selected.Id)
                    : Guarantees.FirstOrDefault(row => row.RootId == selected.RootId);

            if (ReferenceEquals(_selectedTableGuarantee, tableRow))
            {
                return;
            }

            _selectedTableGuarantee = tableRow;
            OnPropertyChanged(nameof(SelectedTableGuarantee));
        }

        private void ResetGuaranteePagination()
        {
            CurrentGuaranteePage = 1;
        }

        private void MoveGuaranteePage(int delta)
        {
            GoToGuaranteePage(CurrentGuaranteePage + delta);
        }

        private void GoToGuaranteePage(object? parameter)
        {
            if (parameter is int pageNumber)
            {
                GoToGuaranteePage(pageNumber);
            }
        }

        private void GoToGuaranteePage(int pageNumber)
        {
            int targetPage = Math.Clamp(pageNumber, 1, TotalGuaranteePages);
            if (targetPage == CurrentGuaranteePage)
            {
                return;
            }

            CurrentGuaranteePage = targetPage;
            Refresh();
        }

        private bool CanGoToGuaranteePage(object? parameter)
        {
            return parameter is int pageNumber
                   && pageNumber >= 1
                   && pageNumber <= TotalGuaranteePages;
        }

        private void RefreshGuaranteePagerButtons()
        {
            GuaranteePagerButtons.Clear();
            foreach (int pageNumber in ReferenceTablePagerController.BuildVisiblePageNumbers(CurrentGuaranteePage, TotalGuaranteePages).Reverse())
            {
                string label = pageNumber.ToString(CultureInfo.InvariantCulture);
                GuaranteePagerButtons.Add(new ReferenceTablePagerButtonItem(
                    pageNumber,
                    label,
                    pageNumber == CurrentGuaranteePage,
                    $"Guarantees.Pager.Page.{label}",
                    $"الصفحة {label}"));
            }

            RaiseGuaranteePagerCommandStates();
        }

        private void SelectGuarantee(GuaranteeRow? row)
        {
            if (row != null)
            {
                LatestInquiryResult = null;
                SelectedGuarantee = row;
                _diagnostics.RecordEvent(
                    "shell.selection",
                    "guarantee.select",
                    new
                    {
                        row.Id,
                        row.RootId,
                        row.GuaranteeNo,
                        row.Supplier,
                        row.Bank
                    });
                WriteDiagnosticsState("select-guarantee");
            }
        }

        private void RefreshAfterWorkflowChange(int rootIdToRestore)
        {
            RefreshAfterWorkflowChange(rootIdToRestore, null);
        }

        private void RefreshAfterWorkflowChange(int rootIdToRestore, int? requestIdToFocus)
        {
            Refresh();
            SelectedGuarantee = rootIdToRestore > 0
                ? ResolveOrSurfaceGuaranteeRow(rootIdToRestore) ?? Guarantees.FirstOrDefault()
                : Guarantees.FirstOrDefault();

            if (requestIdToFocus.HasValue && SelectedGuarantee?.RootId == rootIdToRestore)
            {
                FocusGuaranteeSection(GuaranteeFocusArea.Requests, requestIdToFocus);
            }

            WriteDiagnosticsState("workflow-change");
        }

        private GuaranteeRow? ResolveOrSurfaceGuaranteeRow(int rootId)
        {
            GuaranteeRow? visibleRow = Guarantees.FirstOrDefault(row => row.RootId == rootId);
            if (visibleRow != null)
            {
                return visibleRow;
            }

            Guarantee? currentGuarantee = _database.GetCurrentGuaranteeByRootId(rootId);
            if (currentGuarantee == null)
            {
                return null;
            }

            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            GuaranteeRow surfacedRow = GuaranteeRow.FromGuarantee(currentGuarantee, requests);
            Guarantees.Insert(0, surfacedRow);

            while (Guarantees.Count > PageSize)
            {
                Guarantees.RemoveAt(Guarantees.Count - 1);
            }

            return surfacedRow;
        }

        private void RefreshAfterDataReset()
        {
            LoadFilterOptions();
            Refresh();

            if (_lastFileState.HasLastFile && _database.GetCurrentGuaranteeByRootId(_lastFileState.RootId) == null)
            {
                SetLastFileState(ShellLastFileState.Empty);
            }

            LatestInquiryResult = null;
            WriteDiagnosticsState("data-reset");
        }

        private void LoadFilterOptions()
        {
            GuaranteeWorkspaceFilterData filters = _guaranteeData.BuildFilters(AllBanksLabel, AllTypesLabel);

            TimeStatusOptions.Clear();
            foreach (FilterOption option in filters.TimeStatusOptions)
            {
                TimeStatusOptions.Add(option);
            }

            BankOptions.Clear();
            foreach (string bank in filters.BankOptions)
            {
                BankOptions.Add(bank);
            }

            GuaranteeTypeOptions.Clear();
            foreach (string type in filters.GuaranteeTypeOptions)
            {
                GuaranteeTypeOptions.Add(type);
            }
        }

        private void RefreshSelectedGuaranteeArtifacts()
        {
            GuaranteeSelectionArtifacts artifacts = _guaranteeData.BuildSelectionArtifacts(SelectedGuarantee, _focusedGuaranteeRequestId);
            Timeline.Clear();
            Attachments.Clear();
            GuaranteeRequestsPreview.Clear();
            GuaranteeOutputsPreview.Clear();
            foreach (TimelineItem item in artifacts.Timeline)
            {
                Timeline.Add(item);
            }

            foreach (AttachmentItem item in artifacts.Attachments)
            {
                Attachments.Add(item);
            }

            foreach (GuaranteeRequestPreviewItem item in artifacts.Requests)
            {
                GuaranteeRequestsPreview.Add(item);
            }

            foreach (GuaranteeOutputPreviewItem item in artifacts.Outputs)
            {
                GuaranteeOutputsPreview.Add(item);
            }

            LatestLetterOutput = GuaranteeOutputsPreview.FirstOrDefault(item => item.CanOpenLetter);
            LatestResponseOutput = GuaranteeOutputsPreview.FirstOrDefault(item => item.CanOpenResponse);
            RaiseGuaranteeContextSectionTextProperties();
        }

        private void RaiseGuaranteeContextSectionTextProperties()
        {
            OnPropertyChanged(nameof(GuaranteeRequestsSummaryText));
            OnPropertyChanged(nameof(GuaranteeRequestsContextLabel));
            OnPropertyChanged(nameof(TimelineSummaryText));
            OnPropertyChanged(nameof(TimelineStationsLabel));
            OnPropertyChanged(nameof(OutputsSummaryText));
            OnPropertyChanged(nameof(OutputsAvailabilityLabel));
            OnPropertyChanged(nameof(AttachmentsSummaryText));
            OnPropertyChanged(nameof(HasOutputShortcuts));
            OnPropertyChanged(nameof(OutputShortcutsSummaryText));
        }

        private void RaiseSelectionCommandStates()
        {
            foreach (ICommand command in new[]
                     {
                         EditGuaranteeCommand,
                         CreateExtensionRequestCommand,
                         CreateReleaseRequestCommand,
                         CreateReductionRequestCommand,
                         CreateLiquidationRequestCommand,
                         CreateVerificationRequestCommand,
                         CreateReplacementRequestCommand,
                         RegisterBankResponseCommand,
                         CopyGuaranteeTypeCommand,
                         CopyGuaranteeNoCommand,
                         CopyGuaranteeSupplierCommand,
                         CopyGuaranteeReferenceNumberCommand,
                         CopyGuaranteeIssueDateCommand,
                         CopyGuaranteeExpiryDateCommand,
                         ShowAllAttachmentsCommand,
                         OpenTimelineEvidenceCommand,
                         OpenOutputLetterCommand,
                         OpenOutputResponseCommand
                     })
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void RaiseInquiryCommandStates()
        {
            foreach (ICommand command in new[]
                     {
                         OpenOutputLetterCommand,
                         OpenOutputResponseCommand
                     })
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void RaiseGuaranteePagerCommandStates()
        {
            foreach (ICommand command in new[]
                     {
                         PreviousGuaranteePageCommand,
                         NextGuaranteePageCommand,
                         GoToGuaranteePageCommand
                     })
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void ResetGuaranteeFilters()
        {
            SetGuaranteeFilters(
                string.Empty,
                AllBanksLabel,
                AllTypesLabel,
                FilterOption.AllTimeStatuses);
        }

        private void SetGuaranteeFilters(
            string searchText,
            string selectedBank,
            string selectedGuaranteeType,
            FilterOption selectedTimeStatus)
        {
            bool changed = false;
            changed |= SetProperty(ref _searchText, searchText ?? string.Empty, nameof(SearchText));
            changed |= SetProperty(ref _selectedBank, string.IsNullOrWhiteSpace(selectedBank) ? AllBanksLabel : selectedBank, nameof(SelectedBank));
            changed |= SetProperty(ref _selectedGuaranteeType, string.IsNullOrWhiteSpace(selectedGuaranteeType) ? AllTypesLabel : selectedGuaranteeType, nameof(SelectedGuaranteeType));
            changed |= SetProperty(ref _selectedTimeStatus, selectedTimeStatus, nameof(SelectedTimeStatus));

            if (changed)
            {
                ResetGuaranteePagination();
                Refresh();
            }
        }
    }
}
