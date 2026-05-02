using System;
using System.IO;
using System.Text.Json;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Models
{
    public enum RequestStatus
    {
        Pending,
        Executed,
        Rejected,
        Cancelled,
        Superseded
    }

    public enum RequestType
    {
        Extension,
        Release,
        Liquidation,
        Reduction,
        Verification,
        Replacement
    }

    public class WorkflowRequest
    {
        public int Id { get; set; }
        public int RootGuaranteeId { get; set; } // Reference to the guarantee series
        
        public int SequenceNumber { get; set; } // Order within the same RootId
        public int BaseVersionId { get; set; } // The version ID when request was created
        public int? ResultVersionId { get; set; } // Resulting guarantee/version only when execution creates one.
        
        public RequestType Type { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.Pending;
        
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public DateTime? ResponseRecordedAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        public string RequestedDataJson { get; set; } = string.Empty; // Store changed fields as JSON
        
        public string LetterOriginalFileName { get; set; } = string.Empty;
        public string LetterSavedFileName { get; set; } = string.Empty;
        public string ResponseOriginalFileName { get; set; } = string.Empty;
        public string ResponseSavedFileName { get; set; } = string.Empty;
        public string ResponseNotes { get; set; } = string.Empty;
        
        public string Notes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;

        public string LetterFilePath => string.IsNullOrWhiteSpace(LetterSavedFileName)
            ? string.Empty
            : Path.Combine(AppPaths.WorkflowLettersFolder, LetterSavedFileName);

        public string ResponseFilePath => string.IsNullOrWhiteSpace(ResponseSavedFileName)
            ? string.Empty
            : Path.Combine(AppPaths.WorkflowResponsesFolder, ResponseSavedFileName);

        public bool HasLetter => !string.IsNullOrWhiteSpace(LetterSavedFileName);
        public bool HasResponseDocument => !string.IsNullOrWhiteSpace(ResponseSavedFileName);

        public WorkflowRequestedData? GetRequestedData()
        {
            if (string.IsNullOrWhiteSpace(RequestedDataJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<WorkflowRequestedData>(RequestedDataJson);
            }
            catch
            {
                return null;
            }
        }

        public DateTime? RequestedExpiryDate => GetRequestedData()?.RequestedExpiryDate;
        public decimal? RequestedAmount => GetRequestedData()?.RequestedAmount;
        public string ReplacementGuaranteeNo => GetRequestedData()?.ReplacementGuaranteeNo?.Trim() ?? string.Empty;
        public string ReplacementSupplier => GetRequestedData()?.ReplacementSupplier?.Trim() ?? string.Empty;
        public string ReplacementBank => GetRequestedData()?.ReplacementBank?.Trim() ?? string.Empty;
        public decimal? ReplacementAmount => GetRequestedData()?.ReplacementAmount;
        public DateTime? ReplacementExpiryDate => GetRequestedData()?.ReplacementExpiryDate;
        public GuaranteeDateCalendar ReplacementDateCalendar => GetRequestedData()?.ReplacementDateCalendar ?? DateCalendar;
        public GuaranteeDateCalendar RequestedDateCalendar => GetRequestedData()?.RequestedDateCalendar ?? DateCalendar;
        public GuaranteeDateCalendar DateCalendar { get; set; } = GuaranteeDateCalendar.Gregorian;
        public string ReplacementGuaranteeType => GetRequestedData()?.ReplacementGuaranteeType?.Trim() ?? string.Empty;
        public string ReplacementBeneficiary => GetRequestedData()?.ReplacementBeneficiary?.Trim() ?? string.Empty;
        public GuaranteeReferenceType ReplacementReferenceType => GetRequestedData()?.ReplacementReferenceType ?? GuaranteeReferenceType.None;
        public string ReplacementReferenceNumber => GetRequestedData()?.ReplacementReferenceNumber?.Trim() ?? string.Empty;
        public string RequestedValueLabel => Type switch
        {
            RequestType.Extension => RequestedExpiryDate.HasValue ? DualCalendarDateService.FormatDate(RequestedExpiryDate.Value, RequestedDateCalendar) : "---",
            RequestType.Reduction => RequestedAmount.HasValue ? ArabicAmountFormatter.FormatSaudiRiyals(RequestedAmount.Value) : "---",
            RequestType.Release => "إفراج",
            RequestType.Liquidation => "تسييل",
            RequestType.Verification => "تحقق",
            RequestType.Replacement => string.IsNullOrWhiteSpace(ReplacementGuaranteeNo) ? "استبدال" : ReplacementGuaranteeNo,
            _ => "---"
        };

        // UI Helpers
        public string TypeLabel => Type switch
        {
            RequestType.Extension => "طلب تمديد",
            RequestType.Release => "طلب إفراج",
            RequestType.Liquidation => "طلب تسييل",
            RequestType.Reduction => "طلب تخفيض",
            RequestType.Verification => "طلب تحقق",
            RequestType.Replacement => "طلب استبدال",
            _ => "طلب غير معروف"
        };

        public string StatusLabel => Status switch
        {
            RequestStatus.Pending => "قيد الانتظار",
            RequestStatus.Executed => "منفذ",
            RequestStatus.Rejected => "مرفوض",
            RequestStatus.Cancelled => "مُلغى",
            RequestStatus.Superseded => "مُسقط آليًا",
            _ => "غير معروف"
        };
    }

    public class WorkflowRequestedData
    {
        public DateTime? RequestedExpiryDate { get; set; }
        public GuaranteeDateCalendar RequestedDateCalendar { get; set; } = GuaranteeDateCalendar.Gregorian;
        public decimal? RequestedAmount { get; set; }
        public string ReplacementGuaranteeNo { get; set; } = string.Empty;
        public string ReplacementSupplier { get; set; } = string.Empty;
        public string ReplacementBank { get; set; } = string.Empty;
        public decimal? ReplacementAmount { get; set; }
        public DateTime? ReplacementExpiryDate { get; set; }
        public GuaranteeDateCalendar ReplacementDateCalendar { get; set; } = GuaranteeDateCalendar.Gregorian;
        public string ReplacementGuaranteeType { get; set; } = string.Empty;
        public string ReplacementBeneficiary { get; set; } = string.Empty;
        public GuaranteeReferenceType? ReplacementReferenceType { get; set; }
        public string ReplacementReferenceNumber { get; set; } = string.Empty;
    }
}
