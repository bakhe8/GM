using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public enum TimelineEvidenceActionKind
    {
        None,
        Attachment,
        RequestLetter,
        ResponseDocument,
        OfficialAttachment
    }

    public sealed class TimelineItem
    {
        public TimelineItem(
            DateTime timestamp,
            string title,
            string detail,
            string status,
            Tone tone,
            TimelineEvidenceActionKind evidenceActionKind = TimelineEvidenceActionKind.None,
            AttachmentRecord? evidenceAttachment = null,
            WorkflowRequest? evidenceRequest = null,
            int? evidenceGuaranteeId = null,
            string evidenceKey = "",
            bool isContextTarget = false)
            : this(
                timestamp.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                title,
                detail,
                status,
                tone,
                evidenceActionKind,
                evidenceAttachment,
                evidenceRequest,
                evidenceGuaranteeId,
                evidenceKey,
                isContextTarget)
        {
        }

        public TimelineItem(string date, string title, string detail, string status, Tone tone, bool isContextTarget = false)
            : this(date, string.Empty, title, detail, status, tone, isContextTarget: isContextTarget)
        {
        }

        private TimelineItem(
            string date,
            string time,
            string title,
            string detail,
            string status,
            Tone tone,
            TimelineEvidenceActionKind evidenceActionKind = TimelineEvidenceActionKind.None,
            AttachmentRecord? evidenceAttachment = null,
            WorkflowRequest? evidenceRequest = null,
            int? evidenceGuaranteeId = null,
            string evidenceKey = "",
            bool isContextTarget = false)
        {
            Date = date;
            Time = time;
            Title = title;
            Detail = detail;
            Status = status;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
            IsContextTarget = isContextTarget;
            EvidenceKey = evidenceKey ?? string.Empty;
            EvidenceActionKind = NormalizeEvidenceActionKind(evidenceActionKind, evidenceAttachment, evidenceRequest, evidenceGuaranteeId);
            EvidenceAttachment = EvidenceActionKind == TimelineEvidenceActionKind.Attachment ? evidenceAttachment : null;
            EvidenceRequest = EvidenceActionKind is TimelineEvidenceActionKind.RequestLetter or TimelineEvidenceActionKind.ResponseDocument
                ? evidenceRequest
                : null;
            EvidenceGuaranteeId = EvidenceActionKind == TimelineEvidenceActionKind.OfficialAttachment
                ? evidenceGuaranteeId
                : evidenceGuaranteeId ?? EvidenceAttachment?.GuaranteeId ?? EvidenceRequest?.BaseVersionId;
            EvidenceActionLabel = BuildEvidenceActionLabel(EvidenceActionKind, EvidenceAttachment, EvidenceRequest);
            EvidenceActionHint = BuildEvidenceActionHint(EvidenceActionKind, EvidenceAttachment, EvidenceRequest);
            EvidenceActionAutomationId = BuildEvidenceActionAutomationId(
                EvidenceActionKind,
                EvidenceAttachment,
                EvidenceRequest,
                EvidenceGuaranteeId,
                EvidenceKey);
        }

        public string Date { get; }
        public string Time { get; }
        public string Title { get; }
        public string Detail { get; }
        public string Status { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public bool IsContextTarget { get; }
        public string ContextLabel => IsContextTarget ? "الحدث المفتوح الآن" : string.Empty;
        public string ContextAutomationStatus => IsContextTarget ? "هذا هو حدث الطلب الذي تم فتحه من قائمة أعمال اليوم." : string.Empty;
        public TimelineEvidenceActionKind EvidenceActionKind { get; }
        public string EvidenceKey { get; }
        public AttachmentRecord? EvidenceAttachment { get; }
        public WorkflowRequest? EvidenceRequest { get; }
        public int? EvidenceGuaranteeId { get; }
        public bool HasEvidenceAction => EvidenceActionKind != TimelineEvidenceActionKind.None;
        public bool IsAttachEvidenceAction => EvidenceActionKind == TimelineEvidenceActionKind.OfficialAttachment
            || EvidenceActionKind == TimelineEvidenceActionKind.ResponseDocument && EvidenceRequest?.HasResponseDocument != true;
        public bool IsOpenEvidenceAction => HasEvidenceAction && !IsAttachEvidenceAction;
        public double EvidenceActionWidth => 20d;
        public double EvidenceActionHeight => 20d;
        public string EvidenceActionLabel { get; }
        public string EvidenceActionHint { get; }
        public string EvidenceActionAutomationId { get; }

        public static TimelineItem FromRequest(WorkflowRequest request)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            return new TimelineItem(
                request.RequestDate,
                request.TypeLabel,
                WorkflowRequestDisplayText.BuildDetail(request),
                request.StatusLabel,
                tone);
        }

        public static TimelineItem FromEvent(
            GuaranteeTimelineEvent timelineEvent,
            IReadOnlyDictionary<int, WorkflowRequest>? requestsById = null,
            IReadOnlyDictionary<int, AttachmentRecord>? attachmentsById = null,
            IReadOnlyDictionary<string, AttachmentRecord>? attachmentsByEventKey = null,
            int? focusedRequestId = null)
        {
            ResolveEvidence(
                timelineEvent,
                requestsById,
                attachmentsById,
                attachmentsByEventKey,
                out TimelineEvidenceActionKind evidenceActionKind,
                out AttachmentRecord? evidenceAttachment,
                out WorkflowRequest? evidenceRequest);

            string detail = timelineEvent.Details;
            if (string.Equals(timelineEvent.EventType, "WorkflowRequestCreated", StringComparison.Ordinal)
                && evidenceRequest != null)
            {
                detail = AppendRequestAgeDetail(detail, evidenceRequest);
            }

            return new TimelineItem(
                timelineEvent.OccurredAt,
                timelineEvent.Title,
                detail,
                timelineEvent.Status,
                ParseTone(timelineEvent.ToneKey),
                evidenceActionKind,
                evidenceAttachment,
                evidenceRequest,
                timelineEvent.GuaranteeId,
                timelineEvent.EventKey,
                timelineEvent.WorkflowRequestId.HasValue
                    && focusedRequestId.HasValue
                    && timelineEvent.WorkflowRequestId.Value == focusedRequestId.Value);
        }

        public static TimelineItem RequestCreated(WorkflowRequest request, bool isContextTarget = false)
        {
            string detail = $"القيمة المطلوبة: {request.RequestedValueLabel}";
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                detail += $" | {request.Notes.Trim()}";
            }

            detail = AppendRequestAgeDetail(detail, request);

            return new TimelineItem(
                request.RequestDate,
                request.TypeLabel,
                detail,
                request.Status == RequestStatus.Pending ? request.StatusLabel : "مسجل",
                request.Status == RequestStatus.Pending ? Tone.Warning : Tone.Info,
                TimelineEvidenceActionKind.RequestLetter,
                evidenceRequest: request,
                evidenceKey: $"workflow-request-created:{request.Id.ToString(CultureInfo.InvariantCulture)}",
                isContextTarget: isContextTarget);
        }

        public static TimelineItem BankResponse(WorkflowRequest request, string resultVersionLabel, bool isContextTarget = false)
        {
            string detail = WorkflowRequestDisplayText.BuildDetail(request);
            string effectDetail = BuildBankResponseEffectDetail(request, resultVersionLabel);
            if (!string.IsNullOrWhiteSpace(effectDetail))
            {
                detail += $" | {effectDetail}";
            }

            if (request.HasResponseDocument)
            {
                detail += " | رد البنك مرفق";
            }

            return new TimelineItem(
                request.ResponseRecordedAt!.Value,
                $"تسجيل رد {request.TypeLabel}",
                detail,
                request.StatusLabel,
                GetRequestTone(request.Status),
                TimelineEvidenceActionKind.ResponseDocument,
                evidenceRequest: request,
                evidenceKey: $"workflow-response:{request.Id.ToString(CultureInfo.InvariantCulture)}",
                isContextTarget: isContextTarget);
        }

        public static TimelineItem FromVersion(Guarantee version)
        {
            if (version.VersionNumber <= 1)
            {
                return new TimelineItem(
                    version.CreatedAt,
                    "إنشاء الضمان",
                    $"تم إنشاء الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال وانتهاء {version.ExpiryDate:yyyy/MM/dd}.",
                    "مكتمل",
                    Tone.Success,
                    TimelineEvidenceActionKind.OfficialAttachment,
                    evidenceGuaranteeId: version.Id,
                    evidenceKey: $"guarantee-created:{version.Id.ToString(CultureInfo.InvariantCulture)}");
            }

            return new TimelineItem(
                version.CreatedAt,
                $"الإصدار {version.VersionLabel}",
                $"تم حفظ شروط هذا الإصدار: المبلغ {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال | الانتهاء {version.ExpiryDate:yyyy/MM/dd}.",
                "موثق",
                Tone.Info,
                TimelineEvidenceActionKind.OfficialAttachment,
                evidenceGuaranteeId: version.Id,
                evidenceKey: $"guarantee-version:{version.Id.ToString(CultureInfo.InvariantCulture)}");
        }

        public static TimelineItem AttachmentAdded(AttachmentRecord attachment)
        {
            string documentType = attachment.DocumentTypeLabel;
            string name = string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                ? "مرفق رسمي"
                : attachment.OriginalFileName.Trim();

            return new TimelineItem(
                attachment.UploadedAt,
                $"إضافة مرفق {documentType}",
                name,
                "مضاف",
                Tone.Info,
                TimelineEvidenceActionKind.Attachment,
                evidenceAttachment: attachment,
                evidenceKey: string.IsNullOrWhiteSpace(attachment.SavedFileName)
                    ? $"attachment-added:{attachment.Id.ToString(CultureInfo.InvariantCulture)}"
                    : $"attachment-added:{attachment.GuaranteeId.ToString(CultureInfo.InvariantCulture)}:{attachment.SavedFileName}");
        }

        public static TimelineItem StatusChanged(Guarantee version)
        {
            return new TimelineItem(
                version.CreatedAt,
                IsTerminalLifecycle(version.LifecycleStatus)
                    ? GetTerminalLifecycleTitle(version.LifecycleStatus)
                    : $"تغيير الحالة {version.LifecycleStatusLabel}",
                IsTerminalLifecycle(version.LifecycleStatus)
                    ? GetTerminalLifecycleDetail(version)
                    : $"أصبحت حالة الضمان: {version.LifecycleStatusLabel} ضمن الإصدار {version.VersionLabel}.",
                version.LifecycleStatusLabel,
                GetLifecycleTone(version.LifecycleStatus),
                TimelineEvidenceActionKind.OfficialAttachment,
                evidenceGuaranteeId: version.Id,
                evidenceKey: $"guarantee-status:{version.Id.ToString(CultureInfo.InvariantCulture)}:{version.LifecycleStatus}");
        }

        public static TimelineItem Created(string date)
            => new(date, "إنشاء الضمان", "تم إنشاء الضمان في النظام", "مكتمل", Tone.Success);

        public static TimelineItem VersionCreated(string date, string versionLabel, string status)
            => new(date, $"إنشاء الإصدار {versionLabel}", "تم حفظ هذا الإصدار ضمن سجل الضمان الرسمي", status, Tone.Info);

        private static string AppendRequestAgeDetail(string detail, WorkflowRequest request)
        {
            if (detail.Contains("عمر الطلب", StringComparison.OrdinalIgnoreCase))
            {
                return detail;
            }

            DateTime referenceDate = request.Status == RequestStatus.Pending
                ? DateTime.Today
                : (request.ResponseRecordedAt?.Date ?? DateTime.Today);
            int days = Math.Max(0, (referenceDate - request.RequestDate.Date).Days);
            string ageText = request.Status == RequestStatus.Pending
                ? $"ينتظر {days.ToString("N0", CultureInfo.InvariantCulture)} يوم"
                : $"استغرق {days.ToString("N0", CultureInfo.InvariantCulture)} يوم";

            return $"{detail} | عمر الطلب: {ageText}";
        }

        private static Tone GetRequestTone(RequestStatus status) => status switch
        {
            RequestStatus.Executed => Tone.Success,
            RequestStatus.Pending => Tone.Warning,
            RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
            _ => Tone.Info
        };

        private static Tone ParseTone(string? toneKey)
        {
            return Enum.TryParse(toneKey, ignoreCase: true, out Tone tone)
                ? tone
                : Tone.Info;
        }

        private static void ResolveEvidence(
            GuaranteeTimelineEvent timelineEvent,
            IReadOnlyDictionary<int, WorkflowRequest>? requestsById,
            IReadOnlyDictionary<int, AttachmentRecord>? attachmentsById,
            IReadOnlyDictionary<string, AttachmentRecord>? attachmentsByEventKey,
            out TimelineEvidenceActionKind evidenceActionKind,
            out AttachmentRecord? evidenceAttachment,
            out WorkflowRequest? evidenceRequest)
        {
            evidenceActionKind = TimelineEvidenceActionKind.None;
            evidenceAttachment = null;
            evidenceRequest = null;

            if (!string.IsNullOrWhiteSpace(timelineEvent.EventKey)
                && attachmentsByEventKey?.TryGetValue(timelineEvent.EventKey, out evidenceAttachment) == true)
            {
                evidenceActionKind = TimelineEvidenceActionKind.Attachment;
                return;
            }

            if (timelineEvent.AttachmentId.HasValue
                && attachmentsById?.TryGetValue(timelineEvent.AttachmentId.Value, out evidenceAttachment) == true)
            {
                evidenceActionKind = TimelineEvidenceActionKind.Attachment;
                return;
            }

            if (!timelineEvent.WorkflowRequestId.HasValue
                || requestsById?.TryGetValue(timelineEvent.WorkflowRequestId.Value, out evidenceRequest) != true)
            {
                evidenceRequest = null;
                evidenceActionKind = CanAttachOfficialEvidenceToEvent(timelineEvent.EventType)
                    ? TimelineEvidenceActionKind.OfficialAttachment
                    : TimelineEvidenceActionKind.None;
                return;
            }

            evidenceActionKind = timelineEvent.EventType switch
            {
                "WorkflowRequestCreated" when evidenceRequest?.HasLetter == true => TimelineEvidenceActionKind.RequestLetter,
                "WorkflowRequestCreated" => TimelineEvidenceActionKind.OfficialAttachment,
                "WorkflowResponseRecorded" => TimelineEvidenceActionKind.ResponseDocument,
                "WorkflowResponseDocumentAttached" => TimelineEvidenceActionKind.ResponseDocument,
                _ when CanAttachOfficialEvidenceToEvent(timelineEvent.EventType) => TimelineEvidenceActionKind.OfficialAttachment,
                _ => TimelineEvidenceActionKind.None
            };
        }

        private static TimelineEvidenceActionKind NormalizeEvidenceActionKind(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest,
            int? evidenceGuaranteeId)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment when evidenceAttachment != null =>
                    TimelineEvidenceActionKind.Attachment,
                TimelineEvidenceActionKind.RequestLetter when evidenceRequest?.HasLetter == true =>
                    TimelineEvidenceActionKind.RequestLetter,
                TimelineEvidenceActionKind.ResponseDocument
                    when evidenceRequest != null
                         && (evidenceRequest.HasResponseDocument || evidenceRequest.Status != RequestStatus.Pending) =>
                    TimelineEvidenceActionKind.ResponseDocument,
                TimelineEvidenceActionKind.OfficialAttachment =>
                    TimelineEvidenceActionKind.OfficialAttachment,
                _ => TimelineEvidenceActionKind.None
            };
        }

        private static bool CanAttachOfficialEvidenceToEvent(string eventType)
        {
            return !string.Equals(eventType, "AttachmentAdded", StringComparison.Ordinal)
                   && !string.Equals(eventType, "WorkflowResponseDocumentAttached", StringComparison.Ordinal);
        }

        private static string BuildEvidenceActionLabel(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment => "فتح المرفق",
                TimelineEvidenceActionKind.RequestLetter => "فتح خطاب الطلب",
                TimelineEvidenceActionKind.ResponseDocument when evidenceRequest?.HasResponseDocument == true => "فتح رد البنك",
                TimelineEvidenceActionKind.ResponseDocument => "إرفاق",
                TimelineEvidenceActionKind.OfficialAttachment => "إرفاق",
                _ => string.Empty
            };
        }

        private static string BuildEvidenceActionHint(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment =>
                    $"فتح {evidenceAttachment?.DocumentTypeLabel ?? "المرفق"} المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.RequestLetter =>
                    "فتح خطاب الطلب المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.ResponseDocument when evidenceRequest?.HasResponseDocument == true =>
                    "فتح مستند رد البنك المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.ResponseDocument =>
                    "إرفاق مستند رد البنك بهذا الحدث المغلق.",
                TimelineEvidenceActionKind.OfficialAttachment =>
                    "إرفاق مستند رسمي بهذا الحدث.",
                _ => string.Empty
            };
        }

        private static string BuildEvidenceActionAutomationId(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest,
            int? evidenceGuaranteeId,
            string evidenceKey)
        {
            if (evidenceActionKind == TimelineEvidenceActionKind.None)
            {
                return string.Empty;
            }

            string key = string.IsNullOrWhiteSpace(evidenceKey)
                ? evidenceActionKind switch
                {
                    TimelineEvidenceActionKind.Attachment =>
                        $"attachment:{evidenceAttachment?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.RequestLetter =>
                        $"request-letter:{evidenceRequest?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.ResponseDocument =>
                        $"response-document:{evidenceRequest?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.OfficialAttachment =>
                        $"official-attachment:{evidenceGuaranteeId?.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    _ => "none"
                }
                : evidenceKey;
            string normalized = new string(key
                .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-' || character == ':')
                .ToArray())
                .Replace(':', '.')
                .Replace('-', '.');

            return string.IsNullOrWhiteSpace(normalized)
                ? "GuaranteeTimeline.Evidence"
                : $"GuaranteeTimeline.Evidence.{normalized}";
        }

        private static Tone GetLifecycleTone(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Active => Tone.Success,
            GuaranteeLifecycleStatus.Expired or GuaranteeLifecycleStatus.Liquidated => Tone.Danger,
            GuaranteeLifecycleStatus.Released or GuaranteeLifecycleStatus.Replaced => Tone.Info,
            _ => Tone.Info
        };

        private static string BuildBankResponseEffectDetail(WorkflowRequest request, string resultVersionLabel)
        {
            if (request.Status != RequestStatus.Executed)
            {
                return string.Empty;
            }

            return request.Type switch
            {
                RequestType.Extension when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"الإصدار الناتج: {resultVersionLabel}",
                RequestType.Reduction when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"الإصدار الناتج: {resultVersionLabel}",
                RequestType.Verification when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"اعتماد مستند رسمي على {resultVersionLabel}",
                RequestType.Release =>
                    "تم إنهاء دورة حياة الضمان بالإفراج",
                RequestType.Liquidation =>
                    "تم إنهاء دورة حياة الضمان بالتسييل",
                RequestType.Replacement =>
                    string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                        ? "تم إنشاء ضمان بديل"
                        : $"الضمان البديل: {request.ReplacementGuaranteeNo}",
                _ => string.Empty
            };
        }

        private static bool IsTerminalLifecycle(GuaranteeLifecycleStatus status)
            => status is GuaranteeLifecycleStatus.Released
                or GuaranteeLifecycleStatus.Liquidated
                or GuaranteeLifecycleStatus.Replaced;

        private static string GetTerminalLifecycleTitle(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Released => "إنهاء دورة الحياة بالإفراج",
            GuaranteeLifecycleStatus.Liquidated => "إنهاء دورة الحياة بالتسييل",
            GuaranteeLifecycleStatus.Replaced => "استبدال الضمان",
            _ => "تغيير الحالة"
        };

        private static string GetTerminalLifecycleDetail(Guarantee version) => version.LifecycleStatus switch
        {
            GuaranteeLifecycleStatus.Released =>
                $"تم تسجيل الإفراج عن الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال.",
            GuaranteeLifecycleStatus.Liquidated =>
                $"تم تسجيل تسييل الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال.",
            GuaranteeLifecycleStatus.Replaced =>
                "تم إنهاء السلسلة القديمة لصالح ضمان بديل.",
            _ =>
                $"أصبحت حالة الضمان: {version.LifecycleStatusLabel} ضمن الإصدار {version.VersionLabel}."
        };

    }

    internal static class WorkflowRequestDisplayText
    {
        public static string BuildDetail(WorkflowRequest request)
        {
            string actor = string.IsNullOrWhiteSpace(request.CreatedBy) ? "النظام" : request.CreatedBy;
            if (request.Status == RequestStatus.Pending)
            {
                return $"تم رفع الطلب من قبل {actor}";
            }

            string responseNotes = request.ResponseNotes?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(responseNotes)
                ? "تم تحديث حالة الطلب"
                : responseNotes;
        }

    }
}
