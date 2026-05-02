using System;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Models
{
    public class WorkflowRequestListItem
    {
        public WorkflowRequest Request { get; set; } = new WorkflowRequest();
        public int CurrentGuaranteeId { get; set; }
        public int RootGuaranteeId { get; set; }
        public string GuaranteeNo { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public GuaranteeReferenceType ReferenceType { get; set; } = GuaranteeReferenceType.None;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal CurrentAmount { get; set; }
        public DateTime CurrentExpiryDate { get; set; }
        public int CurrentVersionNumber { get; set; }
        public int BaseVersionNumber { get; set; }
        public int? ResultVersionNumber { get; set; }
        public GuaranteeLifecycleStatus LifecycleStatus { get; set; } = GuaranteeLifecycleStatus.Active;

        public string LifecycleStatusLabel => GuaranteeLifecycleStatusDisplay.GetLabel(LifecycleStatus);

        public string CurrentVersionLabel => GuaranteeVersionDisplay.GetLabel(CurrentVersionNumber);
        public int RelatedVersionNumber => ResultVersionNumber ?? (BaseVersionNumber > 0 ? BaseVersionNumber : CurrentVersionNumber);
        public string RelatedVersionLabel => GuaranteeVersionDisplay.GetLabel(RelatedVersionNumber);
        public string RequestDateLabel => DualCalendarDateService.FormatGregorianDate(Request.RequestDate);
        public string ResponseDateLabel => Request.ResponseRecordedAt.HasValue ? DualCalendarDateService.FormatGregorianDate(Request.ResponseRecordedAt.Value) : "---";
        public bool IsPending => Request.Status == RequestStatus.Pending;
        public bool IsPurchaseOrderOnly => ReferenceType == GuaranteeReferenceType.PurchaseOrder && !string.IsNullOrWhiteSpace(ReferenceNumber);
        public bool IsContractRelated => ReferenceType == GuaranteeReferenceType.Contract && !string.IsNullOrWhiteSpace(ReferenceNumber);
        public string ReferenceTypeLabel => ReferenceType switch
        {
            GuaranteeReferenceType.Contract => "عقد",
            GuaranteeReferenceType.PurchaseOrder => "أمر شراء",
            _ => "بدون مرجع"
        };
        public string CurrentValueFieldLabel => Request.Type switch
        {
            RequestType.Extension => "تاريخ الانتهاء الحالي",
            RequestType.Reduction => "المبلغ الحالي",
            RequestType.Release => "الحالة التشغيلية الحالية",
            RequestType.Liquidation => "الحالة التشغيلية الحالية",
            RequestType.Verification => "الحالة التشغيلية الحالية",
            RequestType.Replacement => "رقم الضمان الحالي",
            _ => "الحقل الحالي"
        };
        public string RequestedValueFieldLabel => Request.Type switch
        {
            RequestType.Extension => "تاريخ الانتهاء المطلوب",
            RequestType.Reduction => "المبلغ المطلوب",
            RequestType.Release => "الإجراء المطلوب",
            RequestType.Liquidation => "الإجراء المطلوب",
            RequestType.Verification => "الإجراء المطلوب",
            RequestType.Replacement => "رقم الضمان البديل",
            _ => "الحقل المطلوب"
        };
        public string CurrentValueLabel => Request.Type switch
        {
            RequestType.Extension => DualCalendarDateService.FormatGregorianDate(CurrentExpiryDate),
            RequestType.Reduction => ArabicAmountFormatter.FormatSaudiRiyals(CurrentAmount),
            RequestType.Release => LifecycleStatusLabel,
            RequestType.Liquidation => LifecycleStatusLabel,
            RequestType.Verification => LifecycleStatusLabel,
            RequestType.Replacement => GuaranteeNo,
            _ => "---"
        };
        public string RequestedValueLabel => Request.RequestedValueLabel;
        public string CurrentValueDisplay => string.IsNullOrWhiteSpace(CurrentValueLabel) ? "---" : CurrentValueLabel;
        public string RequestedValueDisplay => string.IsNullOrWhiteSpace(RequestedValueLabel) ? "---" : RequestedValueLabel;
    }
}
