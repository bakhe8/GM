using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public enum WorkflowRequestQuerySortMode
    {
        DefaultPriorityThenRequestDateDescending,
        RequestDateDescending,
        RequestDateAscending,
        ActivityDateDescending
    }

    public sealed class WorkflowRequestQueryOptions
    {
        public string SearchText { get; set; } = string.Empty;

        public int? RootGuaranteeId { get; set; }

        public RequestType? RequestType { get; set; }

        public RequestStatus? RequestStatus { get; set; }

        public string? Bank { get; set; }

        public string? Supplier { get; set; }

        public string? CreatedBy { get; set; }

        public GuaranteeReferenceType? ReferenceType { get; set; }

        public bool RequireReferenceNumber { get; set; }

        public bool PendingOrMissingResponseOnly { get; set; }

        public DateTime? RequestDateFrom { get; set; }

        public DateTime? RequestDateTo { get; set; }

        public DateTime? ResponseRecordedFrom { get; set; }

        public DateTime? ResponseRecordedTo { get; set; }

        public int? Limit { get; set; }

        public WorkflowRequestQuerySortMode SortMode { get; set; } = WorkflowRequestQuerySortMode.DefaultPriorityThenRequestDateDescending;
    }
}
