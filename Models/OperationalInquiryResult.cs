using System;
using System.Collections.Generic;
using System.Windows;

namespace GuaranteeManager.Models
{
    public sealed class OperationalInquiryResult
    {
        public string InquiryKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public Guarantee? SelectedGuarantee { get; set; }
        public Guarantee? CurrentGuarantee { get; set; }
        public Guarantee? ResultGuarantee { get; set; }
        public WorkflowRequest? RelatedRequest { get; set; }
        public List<OperationalInquiryFact> Facts { get; } = new List<OperationalInquiryFact>();
        public List<OperationalInquiryTimelineEntry> Timeline { get; } = new List<OperationalInquiryTimelineEntry>();

        public string EventDateLabel => EventDate?.ToString("yyyy-MM-dd HH:mm") ?? "---";
        public bool HasTimeline => Timeline.Count > 0;
        public bool CanOpenRequestLetter => RelatedRequest?.HasLetter == true;
        public bool CanOpenResponseDocument => RelatedRequest?.HasResponseDocument == true;
    }

    public sealed class OperationalInquiryFact
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed class OperationalInquiryTimelineEntry
    {
        public DateTime Timestamp { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string TimestampLabel => Timestamp.ToString("yyyy-MM-dd HH:mm");
    }
}
