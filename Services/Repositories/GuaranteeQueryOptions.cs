using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public enum GuaranteeTimeStatus
    {
        Active,
        ExpiringSoon,
        Expired
    }

    public enum GuaranteeQuerySortMode
    {
        CreatedAtDescending,
        ExpiryDateAscendingThenGuaranteeNo
    }

    public sealed class GuaranteeQueryOptions
    {
        public string SearchText { get; set; } = string.Empty;

        public string? Bank { get; set; }

        public string? Supplier { get; set; }

        public string? GuaranteeType { get; set; }

        public GuaranteeTimeStatus? TimeStatus { get; set; }

        public GuaranteeLifecycleStatus? LifecycleStatus { get; set; }

        public IReadOnlyCollection<GuaranteeLifecycleStatus>? LifecycleStatuses { get; set; }

        public IReadOnlyCollection<int>? IncludeRootIds { get; set; }

        public IReadOnlyCollection<int>? ExcludeRootIds { get; set; }

        public IReadOnlyCollection<int>? FollowUpPendingRootIds { get; set; }

        public GuaranteeReferenceType? ReferenceType { get; set; }

        public bool RequireReferenceNumber { get; set; }

        public bool UrgentOnly { get; set; }

        public bool NotExpiredOnly { get; set; }

        public bool NeedsExpiryFollowUpOnly { get; set; }

        public bool IncludeAttachments { get; set; } = true;

        public int? Limit { get; set; }

        public int? Offset { get; set; }

        public GuaranteeQuerySortMode SortMode { get; set; } = GuaranteeQuerySortMode.CreatedAtDescending;
    }
}
