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
        private readonly IContextActionService _contextActionService;

        public GuaranteeWorkspaceDataService(
            IDatabaseService database,
            IContextActionService contextActionService)
        {
            _database = database;
            _contextActionService = contextActionService;
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

        public IReadOnlyList<OperationalInquiryOption> BuildOperationalInquiryOptions()
        {
            var options = new List<OperationalInquiryOption>();
            foreach (ContextActionSection section in GuaranteeInquiryActionSupport.BuildSections(_contextActionService))
            {
                foreach (ContextActionDefinition action in section.Items)
                {
                    if (!action.IsLeaf || string.IsNullOrWhiteSpace(action.Id))
                    {
                        continue;
                    }

                    options.Add(new OperationalInquiryOption(
                        action.Id,
                        section.Header,
                        action.Header,
                        action.Description));
                }
            }

            return options;
        }

        public string BuildOperationalInquiryDescription(OperationalInquiryOption? option, GuaranteeRow? selectedGuarantee)
        {
            if (option == null)
            {
                return "اختر سؤالاً تشغيليًا لعرض جواب مدعوم بالأدلة.";
            }

            string description = option.Description;
            if (selectedGuarantee == null)
            {
                return description;
            }

            Guarantee? current = _database.GetGuaranteeById(selectedGuarantee.Id);
            if (current == null)
            {
                return description;
            }

            ContextActionAvailability availability = GuaranteeInquiryActionSupport.GetAvailability(option.Id, current);
            if (!availability.IsEnabled && !string.IsNullOrWhiteSpace(availability.DisabledReason))
            {
                description = $"{description} {availability.DisabledReason}";
            }

            return description;
        }

        public GuaranteeWorkspaceSnapshot BuildSnapshot(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeTimeStatus? selectedTimeStatus,
            int pageSize)
        {
            List<WorkflowRequestListItem> pendingRequests = _database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });

            List<Guarantee> currentGuarantees = QueryGuarantees(
                searchText,
                selectedBank,
                allBanksLabel,
                selectedGuaranteeType,
                allTypesLabel,
                selectedTimeStatus,
                includeAttachments: true,
                limit: pageSize);

            List<Guarantee> portfolio = QueryGuarantees(
                searchText,
                selectedBank,
                allBanksLabel,
                selectedGuaranteeType,
                allTypesLabel,
                selectedTimeStatus,
                includeAttachments: false,
                limit: null);

            HashSet<int> visibleRootIds = portfolio
                .Select(guarantee => guarantee.RootId ?? guarantee.Id)
                .ToHashSet();

            List<WorkflowRequestListItem> visiblePendingRequests = pendingRequests
                .Where(request => visibleRootIds.Contains(request.RootGuaranteeId))
                .ToList();

            Dictionary<int, List<WorkflowRequest>> requestsByRootId = currentGuarantees
                .Select(guarantee => guarantee.RootId ?? guarantee.Id)
                .Distinct()
                .ToDictionary(
                    rootId => rootId,
                    rootId => _database.GetWorkflowRequestsByRootId(rootId));

            List<GuaranteeRow> rows = currentGuarantees
                .Select(guarantee =>
                {
                    int rootId = guarantee.RootId ?? guarantee.Id;
                    return GuaranteeRow.FromGuarantee(
                        guarantee,
                        requestsByRootId.TryGetValue(rootId, out List<WorkflowRequest>? relatedRequests)
                            ? relatedRequests
                            : new List<WorkflowRequest>());
                })
                .ToList();

            List<Guarantee> active = portfolio
                .Where(guarantee => guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active && !guarantee.IsExpired && !guarantee.IsExpiringSoon)
                .ToList();
            List<Guarantee> expiringSoon = portfolio.Where(guarantee => guarantee.IsExpiringSoon).ToList();
            List<Guarantee> expired = portfolio.Where(guarantee => guarantee.IsExpired).ToList();
            List<Guarantee> expiredFollowUp = portfolio.Where(guarantee => guarantee.NeedsExpiryFollowUp).ToList();
            decimal pendingAmount = visiblePendingRequests
                .GroupBy(request => request.RootGuaranteeId)
                .Select(group => group.First().CurrentAmount)
                .Sum();

            return new GuaranteeWorkspaceSnapshot(
                rows,
                visiblePendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(pendingAmount),
                expired.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expired.Sum(guarantee => guarantee.Amount)),
                expiredFollowUp.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expiredFollowUp.Sum(guarantee => guarantee.Amount)),
                expiringSoon.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(expiringSoon.Sum(guarantee => guarantee.Amount)),
                active.Count.ToString("N0", CultureInfo.InvariantCulture),
                FormatMeta(active.Sum(guarantee => guarantee.Amount)),
                BuildFooterSummary(portfolio.Count, pageSize));
        }

        public GuaranteeSelectionArtifacts BuildSelectionArtifacts(GuaranteeRow? selectedGuarantee)
        {
            if (selectedGuarantee == null)
            {
                return new GuaranteeSelectionArtifacts(
                    new List<TimelineItem>(),
                    new List<AttachmentItem>(),
                    new List<GuaranteeRequestPreviewItem>(),
                    new List<GuaranteeOutputPreviewItem>());
            }

            List<TimelineItem> timeline = new();
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(selectedGuarantee.RootId);
            foreach (WorkflowRequest request in requests
                         .OrderByDescending(request => request.RequestDate)
                         .Take(3))
            {
                timeline.Add(TimelineItem.FromRequest(request));
            }

            if (timeline.Count < 3)
            {
                timeline.Add(TimelineItem.Created(selectedGuarantee.IssueDate));
            }

            List<AttachmentItem> attachments = selectedGuarantee.Attachments
                .Take(3)
                .Select(AttachmentItem.FromAttachment)
                .ToList();

            List<GuaranteeRequestPreviewItem> requestItems = requests
                .OrderByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .Take(4)
                .Select(GuaranteeRequestPreviewItem.FromRequest)
                .ToList();

            List<GuaranteeOutputPreviewItem> outputItems = requests
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
            int? limit)
        {
            return _database.QueryGuarantees(BuildGuaranteeQueryOptions(
                searchText,
                selectedBank,
                allBanksLabel,
                selectedGuaranteeType,
                allTypesLabel,
                selectedTimeStatus,
                includeAttachments,
                limit));
        }

        private static GuaranteeQueryOptions BuildGuaranteeQueryOptions(
            string searchText,
            string selectedBank,
            string allBanksLabel,
            string selectedGuaranteeType,
            string allTypesLabel,
            GuaranteeTimeStatus? selectedTimeStatus,
            bool includeAttachments,
            int? limit)
        {
            return new GuaranteeQueryOptions
            {
                SearchText = searchText,
                Bank = selectedBank == allBanksLabel ? null : selectedBank,
                GuaranteeType = selectedGuaranteeType == allTypesLabel ? null : selectedGuaranteeType,
                TimeStatus = selectedTimeStatus,
                IncludeAttachments = includeAttachments,
                Limit = limit,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            };
        }

        private static string BuildFooterSummary(int totalCount, int pageSize)
        {
            return totalCount == 0
                ? "لا توجد عناصر"
                : $"عرض 1 - {System.Math.Min(pageSize, totalCount).ToString("N0", CultureInfo.InvariantCulture)} من {totalCount.ToString("N0", CultureInfo.InvariantCulture)} عنصر";
        }

        private static string FormatMeta(decimal amount)
        {
            return $"إجمالي القيمة {amount.ToString("N0", CultureInfo.InvariantCulture)} ريال";
        }
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
        string FooterSummary);

    public sealed record GuaranteeSelectionArtifacts(
        IReadOnlyList<TimelineItem> Timeline,
        IReadOnlyList<AttachmentItem> Attachments,
        IReadOnlyList<GuaranteeRequestPreviewItem> Requests,
        IReadOnlyList<GuaranteeOutputPreviewItem> Outputs);
}
