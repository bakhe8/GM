using System;

namespace GuaranteeManager.Models
{
    public sealed class GuaranteeTimelineEvent
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int? GuaranteeId { get; set; }
        public int? WorkflowRequestId { get; set; }
        public int? AttachmentId { get; set; }
        public string EventKey { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public int SortOrder { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ToneKey { get; set; } = "Info";
    }
}
