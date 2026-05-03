using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class GuaranteeWorkspaceDataService
    {
        private readonly IDatabaseService _database;

        public GuaranteeWorkspaceDataService(IDatabaseService database)
        {
            _database = database;
        }

        public GuaranteeWorkspaceFilterData BuildFilters(string allBanksLabel, string allTypesLabel)
        {
            List<FilterOption> timeStatuses = new()
            {
                FilterOption.AllTimeStatuses,
                new FilterOption("نشط", GuaranteeTimeStatus.Active),
                new FilterOption("قريب الانتهاء", GuaranteeTimeStatus.ExpiringSoon),
                new FilterOption("منتهي", GuaranteeTimeStatus.Expired)
            };

            List<string> banks = new() { allBanksLabel };
            banks.AddRange(_database.GetUniqueValues("Bank"));

            List<string> guaranteeTypes = new() { allTypesLabel };
            guaranteeTypes.AddRange(_database.GetUniqueValues("GuaranteeType"));

            return new GuaranteeWorkspaceFilterData(timeStatuses, banks, guaranteeTypes);
        }

        public GuaranteeWorkspaceSnapshot BuildSnapshot(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeStatusFilter selectedStatusFilter,
            int pageSize,
            int pageNumber)
        {
            HashSet<int> pendingRootIds = _database.GetPendingWorkflowRequestRootIds().ToHashSet();

            GuaranteeQueryOptions baseOptions = BuildGuaranteeQueryOptions(
                searchText,
                selectedBank,
                allBanksLabel,
                selectedGuaranteeType,
                allTypesLabel,
                selectedTimeStatus: null,
                includeAttachments: false,
                limit: null,
                offset: null);

            GuaranteeQueryOptions activeOptions = CloneQueryOptions(
                baseOptions,
                timeStatus: GuaranteeTimeStatus.Active,
                lifecycleStatus: GuaranteeLifecycleStatus.Active);
            GuaranteeQueryOptions expiringSoonOptions = CloneQueryOptions(
                baseOptions,
                timeStatus: GuaranteeTimeStatus.ExpiringSoon,
                lifecycleStatus: GuaranteeLifecycleStatus.Active,
                excludeRootIds: pendingRootIds);
            GuaranteeQueryOptions expiredOptions = CloneQueryOptions(
                baseOptions,
                timeStatus: GuaranteeTimeStatus.Expired,
                lifecycleStatuses: new[]
                {
                    GuaranteeLifecycleStatus.Released,
                    GuaranteeLifecycleStatus.Liquidated,
                    GuaranteeLifecycleStatus.Replaced,
                    GuaranteeLifecycleStatus.Closed
                },
                excludeRootIds: pendingRootIds);
            GuaranteeQueryOptions followUpOptions = CloneQueryOptions(
                baseOptions,
                needsExpiryFollowUpOnly: true,
                followUpPendingRootIds: pendingRootIds);

            GuaranteeQueryOptions selectedOptions = selectedStatusFilter switch
            {
                GuaranteeStatusFilter.Active => activeOptions,
                GuaranteeStatusFilter.ExpiringSoon => expiringSoonOptions,
                GuaranteeStatusFilter.NeedsFollowUp => followUpOptions,
                GuaranteeStatusFilter.Expired => expiredOptions,
                _ => baseOptions
            };

            int totalCount = _database.CountGuarantees(selectedOptions);
            int totalPages = ReferenceTablePagerController.CalculateTotalPages(totalCount, pageSize);
            int currentPage = System.Math.Clamp(pageNumber, 1, totalPages);
            int offset = (currentPage - 1) * pageSize;

            List<Guarantee> currentGuarantees = _database.QueryGuarantees(CloneQueryOptions(
                selectedOptions,
                includeAttachments: false,
                limit: pageSize,
                offset: offset));

            HashSet<int> pendingVisibleRootIds = pendingRootIds.Count == 0
                ? new HashSet<int>()
                : _database.QueryGuarantees(CloneQueryOptions(
                        baseOptions,
                        includeRootIds: pendingRootIds,
                        includeAttachments: false))
                .Select(guarantee => guarantee.RootId ?? guarantee.Id)
                .ToHashSet();

            List<WorkflowRequestListItem> visiblePendingRequests = pendingVisibleRootIds.Count == 0
                ? new List<WorkflowRequestListItem>()
                : _database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestStatus = RequestStatus.Pending,
                    RootGuaranteeIds = pendingVisibleRootIds,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
                });

            List<int> currentRootIds = currentGuarantees
                .Select(guarantee => guarantee.RootId ?? guarantee.Id)
                .Distinct()
                .ToList();
            Dictionary<int, List<WorkflowRequest>> requestsByRootId = _database.GetWorkflowRequestsByRootIds(currentRootIds);
            Dictionary<int, IReadOnlyList<AttachmentRecord>> attachmentsByRootId = _database.GetSeriesAttachmentsByRootIds(
                currentRootIds);

            List<GuaranteeRow> rows = currentGuarantees
                .Select(guarantee =>
                {
                    int rootId = guarantee.RootId ?? guarantee.Id;
                    List<WorkflowRequest> relatedRequests = requestsByRootId.TryGetValue(rootId, out List<WorkflowRequest>? rootRequests)
                        ? rootRequests
                        : new List<WorkflowRequest>();

                    GuaranteeRow row = GuaranteeRow.FromGuarantee(
                        guarantee,
                        relatedRequests);
                    if (attachmentsByRootId.TryGetValue(rootId, out IReadOnlyList<AttachmentRecord>? attachments))
                    {
                        row.SetAttachments(attachments);
                    }

                    return row;
                })
                .ToList();

            decimal pendingAmount = visiblePendingRequests
                .GroupBy(request => request.RootGuaranteeId)
                .Select(group => group.First().CurrentAmount)
                .Sum();
            int expiredCount = _database.CountGuarantees(expiredOptions);
            decimal expiredAmount = _database.SumGuaranteeAmounts(expiredOptions);
            int expiredFollowUpCount = _database.CountGuarantees(followUpOptions);
            decimal expiredFollowUpAmount = _database.SumGuaranteeAmounts(followUpOptions);
            int expiringSoonCount = _database.CountGuarantees(expiringSoonOptions);
            decimal expiringSoonAmount = _database.SumGuaranteeAmounts(expiringSoonOptions);
            int activeCount = _database.CountGuarantees(activeOptions);
            decimal activeAmount = _database.SumGuaranteeAmounts(activeOptions);

            return new GuaranteeWorkspaceSnapshot(
                rows,
                visiblePendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(pendingAmount),
                expiredCount.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expiredAmount),
                expiredFollowUpCount.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expiredFollowUpAmount),
                expiringSoonCount.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expiringSoonAmount),
                activeCount.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(activeAmount),
                currentPage,
                totalPages,
                ReferenceTablePagerController.BuildSummary(totalCount, pageSize, currentPage, "عنصر"));
        }

        public GuaranteeSelectionArtifacts BuildSelectionArtifacts(GuaranteeRow? selectedGuarantee, int? focusedRequestId = null)
        {
            if (selectedGuarantee == null)
            {
                return new GuaranteeSelectionArtifacts(
                    new List<TimelineItem>(),
                    new List<AttachmentItem>(),
                    new List<GuaranteeRequestPreviewItem>(),
                    new List<GuaranteeOutputPreviewItem>());
            }

            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(selectedGuarantee.RootId);
            List<Guarantee> history = _database.GetGuaranteeHistory(selectedGuarantee.Id);
            List<GuaranteeTimelineEvent> storedEvents = _database.GetGuaranteeTimelineEvents(selectedGuarantee.Id);
            List<WorkflowRequest> contextRequests = selectedGuarantee.IsCurrentVersion
                ? requests
                : requests
                    .Where(request => request.BaseVersionId == selectedGuarantee.Id || request.ResultVersionId == selectedGuarantee.Id)
                    .ToList();
            List<TimelineItem> timeline = BuildTimeline(selectedGuarantee, history, contextRequests, storedEvents, focusedRequestId);

            List<AttachmentItem> attachments = selectedGuarantee.Attachments
                .Take(3)
                .Select(AttachmentItem.FromAttachment)
                .ToList();

            List<WorkflowRequest> orderedRequests = contextRequests
                .OrderByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .ToList();

            List<WorkflowRequest> requestPreviewSource = orderedRequests.Take(4).ToList();
            if (focusedRequestId.HasValue && requestPreviewSource.All(request => request.Id != focusedRequestId.Value))
            {
                WorkflowRequest? focusedRequest = orderedRequests.FirstOrDefault(request => request.Id == focusedRequestId.Value);
                if (focusedRequest != null)
                {
                    requestPreviewSource = orderedRequests
                        .Where(request => request.Id != focusedRequestId.Value)
                        .Take(3)
                        .Prepend(focusedRequest)
                        .ToList();
                }
            }

            List<GuaranteeRequestPreviewItem> requestItems = requestPreviewSource
                .Select(request => GuaranteeRequestPreviewItem.FromRequest(
                    request,
                    focusedRequestId.HasValue && request.Id == focusedRequestId.Value))
                .ToList();

            List<GuaranteeOutputPreviewItem> outputItems = contextRequests
                .Where(request => request.HasLetter || request.HasResponseDocument)
                .OrderByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .Take(4)
                .Select(GuaranteeOutputPreviewItem.FromRequest)
                .ToList();

            return new GuaranteeSelectionArtifacts(timeline, attachments, requestItems, outputItems);
        }

        public List<Guarantee> QueryGuarantees(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeTimeStatus? selectedTimeStatus,
            bool includeAttachments,
            int? limit,
            int? offset = null)
        {
            return _database.QueryGuarantees(BuildGuaranteeQueryOptions(
                searchText,
                selectedBank,
                allBanksLabel,
                selectedGuaranteeType,
                allTypesLabel,
                selectedTimeStatus,
                includeAttachments,
                limit,
                offset));
        }

        private static GuaranteeQueryOptions BuildGuaranteeQueryOptions(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeTimeStatus? selectedTimeStatus,
            bool includeAttachments,
            int? limit,
            int? offset,
            GuaranteeLifecycleStatus? lifecycleStatus = null,
            IReadOnlyCollection<GuaranteeLifecycleStatus>? lifecycleStatuses = null,
            IReadOnlyCollection<int>? includeRootIds = null,
            IReadOnlyCollection<int>? excludeRootIds = null,
            IReadOnlyCollection<int>? followUpPendingRootIds = null,
            bool needsExpiryFollowUpOnly = false)
        {
            return new GuaranteeQueryOptions
            {
                SearchText = searchText,
                Bank = selectedBank == allBanksLabel ? null : selectedBank,
                GuaranteeType = selectedGuaranteeType == allTypesLabel ? null : selectedGuaranteeType,
                TimeStatus = selectedTimeStatus,
                LifecycleStatus = lifecycleStatus,
                LifecycleStatuses = lifecycleStatuses,
                IncludeRootIds = includeRootIds,
                ExcludeRootIds = excludeRootIds,
                FollowUpPendingRootIds = followUpPendingRootIds,
                NeedsExpiryFollowUpOnly = needsExpiryFollowUpOnly,
                IncludeAttachments = includeAttachments,
                Limit = limit,
                Offset = offset,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            };
        }

        private static GuaranteeQueryOptions CloneQueryOptions(
            GuaranteeQueryOptions source,
            GuaranteeTimeStatus? timeStatus = null,
            GuaranteeLifecycleStatus? lifecycleStatus = null,
            IReadOnlyCollection<GuaranteeLifecycleStatus>? lifecycleStatuses = null,
            IReadOnlyCollection<int>? includeRootIds = null,
            IReadOnlyCollection<int>? excludeRootIds = null,
            IReadOnlyCollection<int>? followUpPendingRootIds = null,
            bool? needsExpiryFollowUpOnly = null,
            bool? includeAttachments = null,
            int? limit = null,
            int? offset = null)
        {
            return new GuaranteeQueryOptions
            {
                SearchText = source.SearchText,
                Bank = source.Bank,
                Supplier = source.Supplier,
                GuaranteeType = source.GuaranteeType,
                TimeStatus = timeStatus ?? source.TimeStatus,
                LifecycleStatus = lifecycleStatus ?? source.LifecycleStatus,
                LifecycleStatuses = lifecycleStatuses ?? source.LifecycleStatuses,
                IncludeRootIds = includeRootIds ?? source.IncludeRootIds,
                ExcludeRootIds = excludeRootIds ?? source.ExcludeRootIds,
                FollowUpPendingRootIds = followUpPendingRootIds ?? source.FollowUpPendingRootIds,
                ReferenceType = source.ReferenceType,
                RequireReferenceNumber = source.RequireReferenceNumber,
                UrgentOnly = source.UrgentOnly,
                NotExpiredOnly = source.NotExpiredOnly,
                NeedsExpiryFollowUpOnly = needsExpiryFollowUpOnly ?? source.NeedsExpiryFollowUpOnly,
                IncludeAttachments = includeAttachments ?? source.IncludeAttachments,
                Limit = limit,
                Offset = offset,
                SortMode = source.SortMode
            };
        }

        private static IEnumerable<Guarantee> ApplyStatusFilter(
            IEnumerable<Guarantee> portfolio,
            GuaranteeStatusFilter selectedStatusFilter,
            IReadOnlySet<int> pendingRootIds)
        {
            return selectedStatusFilter switch
            {
                GuaranteeStatusFilter.Active => portfolio.Where(guarantee =>
                    guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active &&
                    !guarantee.IsExpired &&
                    !guarantee.IsExpiringSoon),
                GuaranteeStatusFilter.ExpiringSoon => portfolio.Where(guarantee =>
                    IsExpiringSoonWithoutPendingRequest(guarantee, pendingRootIds)),
                GuaranteeStatusFilter.NeedsFollowUp => portfolio.Where(guarantee =>
                    NeedsGuaranteeFollowUp(guarantee, pendingRootIds)),
                GuaranteeStatusFilter.Expired => portfolio.Where(guarantee =>
                    IsClosedExpiredWithoutPendingRequest(guarantee, pendingRootIds)),
                _ => portfolio
            };
        }

        private static HashSet<int> BuildPendingRootIds(IReadOnlyList<WorkflowRequestListItem> pendingRequests)
            => pendingRequests
                .Where(request => request.Request.Status == RequestStatus.Pending)
                .Select(request => request.RootGuaranteeId)
                .ToHashSet();

        private static bool IsExpiringSoonWithoutPendingRequest(
            Guarantee guarantee,
            IReadOnlySet<int> pendingRootIds)
            => IsActionableExpiringSoon(guarantee) && !pendingRootIds.Contains(GetRootId(guarantee));

        private static bool IsExpiringSoonWithPendingRequest(
            Guarantee guarantee,
            IReadOnlySet<int> pendingRootIds)
            => IsActionableExpiringSoon(guarantee) && pendingRootIds.Contains(GetRootId(guarantee));

        private static bool IsActionableExpiringSoon(Guarantee guarantee)
            => guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active && guarantee.IsExpiringSoon;

        private static bool NeedsGuaranteeFollowUp(
            Guarantee guarantee,
            IReadOnlySet<int> pendingRootIds)
            => guarantee.NeedsExpiryFollowUp || IsExpiringSoonWithPendingRequest(guarantee, pendingRootIds);

        private static int GetRootId(Guarantee guarantee) => guarantee.RootId ?? guarantee.Id;

        private static bool IsClosedExpiredGuarantee(Guarantee guarantee)
            => guarantee.IsExpired &&
               (guarantee.LifecycleStatus is GuaranteeLifecycleStatus.Released
                   or GuaranteeLifecycleStatus.Liquidated
                   or GuaranteeLifecycleStatus.Replaced
                   or GuaranteeLifecycleStatus.Closed);

        private static bool IsClosedExpiredWithoutPendingRequest(
            Guarantee guarantee,
            IReadOnlySet<int> pendingRootIds)
            => IsClosedExpiredGuarantee(guarantee) && !pendingRootIds.Contains(GetRootId(guarantee));

        private static string FormatMeta(decimal amount)
        {
            return $"إجمالي المبلغ {ArabicAmountFormatter.FormatSaudiRiyals(amount)}";
        }

        private static List<TimelineItem> BuildTimeline(
            GuaranteeRow selectedGuarantee,
            IReadOnlyList<Guarantee> history,
            IReadOnlyList<WorkflowRequest> contextRequests,
            IReadOnlyList<GuaranteeTimelineEvent> storedEvents,
            int? focusedRequestId)
        {
            List<GuaranteeTimelineEvent> selectedEvents = (selectedGuarantee.IsCurrentVersion
                ? storedEvents
                : storedEvents.Where(item => item.GuaranteeId == selectedGuarantee.Id))
                .OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.SortOrder)
                .ThenByDescending(item => item.Id)
                .ToList();
            if (selectedEvents.Count > 0)
            {
                Dictionary<int, WorkflowRequest> requestsById = contextRequests.ToDictionary(request => request.Id);
                Dictionary<int, AttachmentRecord> attachmentsById = history
                    .SelectMany(version => version.Attachments)
                    .GroupBy(attachment => attachment.Id)
                    .ToDictionary(group => group.Key, group => group.First());
                Dictionary<string, AttachmentRecord> attachmentsByEventKey = history
                    .SelectMany(version => version.Attachments)
                    .Where(attachment => !string.IsNullOrWhiteSpace(attachment.TimelineEventKey))
                    .GroupBy(attachment => attachment.TimelineEventKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(attachment => attachment.UploadedAt).First(),
                        StringComparer.OrdinalIgnoreCase);

                return selectedEvents
                    .Select(item => TimelineItem.FromEvent(item, requestsById, attachmentsById, attachmentsByEventKey, focusedRequestId, selectedGuarantee.DateCalendar))
                    .ToList();
            }

            Dictionary<int, Guarantee> versionsById = history.ToDictionary(version => version.Id);
            List<Guarantee> versionEvents = (selectedGuarantee.IsCurrentVersion
                ? history
                : history.Where(version => version.Id == selectedGuarantee.Id))
                .OrderBy(version => version.VersionNumber)
                .ThenBy(version => version.CreatedAt)
                .ThenBy(version => version.Id)
                .ToList();

            var events = new List<TimelineEvent>();
            foreach (Guarantee version in versionEvents)
            {
                int priority = version.VersionNumber <= 1 ? 10 : 50;
                events.Add(new TimelineEvent(version.CreatedAt, priority, TimelineItem.FromVersion(version)));
            }

            foreach (Guarantee version in BuildStatusChangeVersions(versionEvents, contextRequests))
            {
                events.Add(new TimelineEvent(version.CreatedAt, 60, TimelineItem.StatusChanged(version)));
            }

            foreach (AttachmentRecord attachment in BuildAttachmentEventSource(versionEvents))
            {
                events.Add(new TimelineEvent(attachment.UploadedAt, 40, TimelineItem.AttachmentAdded(attachment, selectedGuarantee.DateCalendar)));
            }

            foreach (WorkflowRequest request in contextRequests)
            {
                bool isContextTarget = focusedRequestId.HasValue && request.Id == focusedRequestId.Value;
                events.Add(new TimelineEvent(request.RequestDate, 20, TimelineItem.RequestCreated(request, isContextTarget)));
                if (request.ResponseRecordedAt.HasValue)
                {
                    string resultVersionLabel = string.Empty;
                    if (request.ResultVersionId.HasValue
                        && versionsById.TryGetValue(request.ResultVersionId.Value, out Guarantee? resultVersion))
                    {
                        resultVersionLabel = resultVersion.VersionLabel;
                    }

                    events.Add(new TimelineEvent(
                        request.ResponseRecordedAt.Value,
                        30,
                        TimelineItem.BankResponse(request, resultVersionLabel, isContextTarget)));
                }
            }

            if (events.Count == 0)
            {
                events.Add(new TimelineEvent(
                    selectedGuarantee.ExpiryDateValue,
                    0,
                    selectedGuarantee.IsCurrentVersion
                        ? TimelineItem.Created(selectedGuarantee.IssueDate)
                        : TimelineItem.VersionCreated(selectedGuarantee.IssueDate, selectedGuarantee.VersionLabel, selectedGuarantee.WorkStatus)));
            }

            return events
                .OrderByDescending(item => item.Timestamp)
                .ThenByDescending(item => item.Priority)
                .Select(item => item.Item)
                .ToList();
        }

        private static IEnumerable<Guarantee> BuildStatusChangeVersions(
            IReadOnlyList<Guarantee> versions,
            IReadOnlyCollection<WorkflowRequest> requests)
        {
            Guarantee? previous = null;
            foreach (Guarantee version in versions)
            {
                if ((previous != null && version.LifecycleStatus != previous.LifecycleStatus)
                    || (previous == null && IsTerminalLifecycle(version.LifecycleStatus)))
                {
                    if (!IsLifecycleExplainedByWorkflowResponse(version, requests))
                    {
                        yield return version;
                    }
                }

                previous = version;
            }
        }

        private static bool IsLifecycleExplainedByWorkflowResponse(
            Guarantee version,
            IReadOnlyCollection<WorkflowRequest> requests)
        {
            RequestType? expectedType = version.LifecycleStatus switch
            {
                GuaranteeLifecycleStatus.Released => RequestType.Release,
                GuaranteeLifecycleStatus.Liquidated => RequestType.Liquidation,
                GuaranteeLifecycleStatus.Replaced => RequestType.Replacement,
                _ => null
            };

            if (!expectedType.HasValue)
            {
                return false;
            }

            return requests.Any(request =>
                request.Type == expectedType.Value
                && request.Status == RequestStatus.Executed
                && request.ResponseRecordedAt.HasValue
                && (request.BaseVersionId == version.Id || request.ResultVersionId == version.Id));
        }

        private static bool IsTerminalLifecycle(GuaranteeLifecycleStatus status)
            => status is GuaranteeLifecycleStatus.Released
                or GuaranteeLifecycleStatus.Liquidated
                or GuaranteeLifecycleStatus.Replaced;

        private static IEnumerable<AttachmentRecord> BuildAttachmentEventSource(IReadOnlyList<Guarantee> versions)
        {
            return versions
                .SelectMany(version => version.Attachments)
                .GroupBy(
                    attachment => string.IsNullOrWhiteSpace(attachment.SavedFileName)
                        ? $"attachment:{attachment.Id.ToString(CultureInfo.InvariantCulture)}"
                        : attachment.SavedFileName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(attachment => attachment.UploadedAt)
                    .ThenBy(attachment => attachment.Id)
                    .First())
                .OrderBy(attachment => attachment.UploadedAt)
                .ThenBy(attachment => attachment.Id);
        }

        private sealed record TimelineEvent(DateTime Timestamp, int Priority, TimelineItem Item);
    }

    public sealed record GuaranteeWorkspaceFilterData(
        IReadOnlyList<FilterOption> TimeStatusOptions,
        IReadOnlyList<string> BankOptions,
        IReadOnlyList<string> GuaranteeTypeOptions);

    public sealed record GuaranteeWorkspaceSnapshot(
        IReadOnlyList<GuaranteeRow> Rows,
        string PendingRequestCount,
        string PendingRequestMeta,
        string ExpiredCount,
        string ExpiredMeta,
        string ExpiredFollowUpCount,
        string ExpiredFollowUpMeta,
        string ExpiringSoonCount,
        string ExpiringSoonMeta,
        string ActiveCount,
        string ActiveMeta,
        int CurrentPage,
        int TotalPages,
        string FooterSummary);

    public sealed record GuaranteeSelectionArtifacts(
        IReadOnlyList<TimelineItem> Timeline,
        IReadOnlyList<AttachmentItem> Attachments,
        IReadOnlyList<GuaranteeRequestPreviewItem> Requests,
        IReadOnlyList<GuaranteeOutputPreviewItem> Outputs);
}
