using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class GuaranteeRequestPreviewItem
    {
        public GuaranteeRequestPreviewItem(
            WorkflowRequest request,
            string requestNo,
            string requestType,
            string date,
            string detail,
            string status,
            string requestedValue,
            Tone tone,
            bool isContextTarget)
        {
            Request = request;
            RequestNo = requestNo;
            RequestType = requestType;
            Date = date;
            Detail = detail;
            Status = status;
            RequestedValue = requestedValue;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
            IsContextTarget = isContextTarget;
        }

        public WorkflowRequest Request { get; }
        public string RequestNo { get; }
        public string RequestType { get; }
        public string RequestHeading => IsContextTarget ? $"{RequestType} • الطلب المفتوح الآن" : RequestType;
        public string Date { get; }
        public string Detail { get; }
        public string Status { get; }
        public string RequestedValue { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public bool IsContextTarget { get; }
        public string ContextLabel => IsContextTarget ? "الطلب المفتوح الآن" : string.Empty;
        public string ContextAutomationStatus => IsContextTarget ? "هذا هو الطلب الذي تم فتح السجل الزمني عليه." : string.Empty;
        public bool CanRegisterResponse => Request.Status == RequestStatus.Pending;
        public bool CanOpenLetter => Request.HasLetter;
        public bool CanOpenResponse => Request.HasResponseDocument;

        public static GuaranteeRequestPreviewItem FromRequest(WorkflowRequest request, bool isContextTarget = false)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            string detail = request.Status == RequestStatus.Pending || !string.IsNullOrWhiteSpace(request.ResponseNotes)
                ? WorkflowRequestDisplayText.BuildDetail(request)
                : $"آخر تحديث بواسطة {(string.IsNullOrWhiteSpace(request.CreatedBy) ? "النظام" : request.CreatedBy)}";

            return new GuaranteeRequestPreviewItem(
                request,
                $"REQ-{request.Id:0000}",
                request.TypeLabel,
                DualCalendarDateService.FormatGregorianDate(request.RequestDate),
                detail,
                request.StatusLabel,
                request.RequestedValueLabel,
                tone,
                isContextTarget);
        }
    }

    public sealed class GuaranteeOutputPreviewItem
    {
        public GuaranteeOutputPreviewItem(WorkflowRequest request, string requestNo, string title, string date, string detail, string status, Tone tone)
        {
            Request = request;
            RequestNo = requestNo;
            Title = title;
            Date = date;
            Detail = detail;
            Status = status;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
        }

        public WorkflowRequest Request { get; }
        public string RequestNo { get; }
        public string Title { get; }
        public string Date { get; }
        public string Detail { get; }
        public string Status { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public bool CanOpenLetter => Request.HasLetter;
        public bool CanOpenResponse => Request.HasResponseDocument;

        public static GuaranteeOutputPreviewItem FromRequest(WorkflowRequest request)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            string detail = request.HasLetter && request.HasResponseDocument
                ? "يتوفر خطاب الطلب ورد البنك لهذا الطلب."
                : request.HasLetter
                    ? "يتوفر خطاب الطلب لهذا الطلب."
                    : "يتوفر رد البنك لهذا الطلب.";

            return new GuaranteeOutputPreviewItem(
                request,
                $"REQ-{request.Id:0000}",
                request.TypeLabel,
                DualCalendarDateService.FormatGregorianDate(request.RequestDate),
                detail,
                request.StatusLabel,
                tone);
        }
    }
}
