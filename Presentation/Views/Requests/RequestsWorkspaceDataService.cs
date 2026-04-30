using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class RequestsWorkspaceDataService
    {
        public RequestsWorkspaceMetrics BuildMetrics(IReadOnlyList<WorkflowRequestListItem> requests)
        {
            return new RequestsWorkspaceMetrics(
                requests.Count.ToString("N0", CultureInfo.InvariantCulture),
                requests.Count(item => item.Request.Status == RequestStatus.Pending).ToString("N0", CultureInfo.InvariantCulture),
                requests.Count(item => item.Request.Status != RequestStatus.Pending && !item.Request.HasResponseDocument).ToString("N0", CultureInfo.InvariantCulture),
                requests.Count(item => item.Request.Status != RequestStatus.Pending).ToString("N0", CultureInfo.InvariantCulture));
        }

        public RequestsWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<WorkflowRequestListItem> allRequests,
            string searchText,
            RequestStatus? status)
        {
            IEnumerable<WorkflowRequestListItem> query = allRequests;
            if (status.HasValue)
            {
                query = query.Where(item => item.Request.Status == status.Value);
            }

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.GuaranteeNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Supplier.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Bank.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Request.TypeLabel.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Request.StatusLabel.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            List<RequestListDisplayItem> items = query
                .Select(item => new RequestListDisplayItem(item))
                .ToList();

            return new RequestsWorkspaceFilterResult(items);
        }

        public RequestsWorkspaceDetailState BuildDetailState(RequestListDisplayItem? selected)
        {
            if (selected == null)
            {
                return new RequestsWorkspaceDetailState(
                    "اختر طلبًا",
                    "ستظهر تفاصيل الطلب المحدد هنا.",
                    "---",
                    null,
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"),
                    "---",
                    "اختر طلبًا لعرض خطاب الطلب.",
                    "---",
                    "اختر طلبًا لعرض رد البنك.",
                    "اختر طلبًا من الجدول لرفع أو إلحاق رد البنك عند الحاجة.",
                    false,
                    false,
                    false,
                    false,
                    "إرفاق رد البنك",
                    "اختر طلبًا أولًا.",
                    false,
                    "الإجراء التالي",
                    "اختر طلبًا لعرض الإجراء المتوقع الآن.");
            }

            WorkflowRequestListItem item = selected.Item;
            WorkflowRequest request = item.Request;

            return new RequestsWorkspaceDetailState(
                $"{item.GuaranteeNo} - {request.TypeLabel}",
                item.Supplier,
                item.Bank,
                selected.BankLogo,
                request.StatusLabel,
                selected.RequestStatusBrush,
                StatusBackgroundBrush(request.Status),
                StatusBorderBrush(request.Status),
                BuildLetterAttachmentTitle(request),
                request.HasLetter
                    ? $"خطاب الطلب محفوظ بتاريخ {item.RequestDateLabel}."
                    : "لا يوجد خطاب طلب محفوظ لهذا الطلب.",
                BuildResponseAttachmentTitle(request),
                BuildResponseAttachmentMeta(request),
                BuildResponseAttachHint(selected),
                true,
                request.HasLetter,
                request.HasResponseDocument,
                !request.HasResponseDocument && selected.CanRunQueueAction,
                request.HasResponseDocument
                    ? "رد البنك مرفق"
                    : request.Status == RequestStatus.Pending
                        ? "تسجيل رد البنك"
                        : "إلحاق رد البنك",
                request.Status == RequestStatus.Pending
                    ? "سجل قرار البنك وأرفق المستند عند توفره."
                    : request.HasResponseDocument
                        ? "رد البنك مرفق ويمكن فتحه من بطاقة المرفقات."
                        : "ألحق مستند رد البنك لهذا الطلب المغلق.",
                selected.CanRunQueueAction,
                selected.QueueActionLabel,
                selected.QueueActionHint);
        }

        private static string BuildLetterAttachmentTitle(WorkflowRequest request)
        {
            if (!request.HasLetter)
            {
                return "غير متاح";
            }

            return string.IsNullOrWhiteSpace(request.LetterOriginalFileName)
                ? "خطاب الطلب محفوظ"
                : request.LetterOriginalFileName;
        }

        private static string BuildResponseAttachmentTitle(WorkflowRequest request)
        {
            if (!request.HasResponseDocument)
            {
                return "غير مرفق";
            }

            return string.IsNullOrWhiteSpace(request.ResponseOriginalFileName)
                ? "رد البنك محفوظ"
                : request.ResponseOriginalFileName;
        }

        private static string BuildResponseAttachmentMeta(WorkflowRequest request)
        {
            if (!request.HasResponseDocument)
            {
                return request.Status == RequestStatus.Pending
                    ? "لم يصل رد البنك بعد."
                    : "يمكن إلحاق مستند الرد من البطاقة التالية.";
            }

            return request.ResponseRecordedAt.HasValue
                ? $"رد البنك محفوظ بتاريخ {request.ResponseRecordedAt.Value:yyyy-MM-dd}."
                : "رد البنك محفوظ لهذا الطلب.";
        }

        private static string BuildResponseAttachHint(RequestListDisplayItem selected)
        {
            WorkflowRequest request = selected.Item.Request;
            if (request.HasResponseDocument)
            {
                return "يوجد رد بنك محفوظ لهذا الطلب. استخدم بطاقة المرفقات لفتحه.";
            }

            return request.Status == RequestStatus.Pending
                ? "عند وصول رد البنك، سجل القرار وأرفق المستند من هنا."
                : "ألحق مستند رد البنك الخاص بهذا الطلب من هنا.";
        }

        private static Brush StatusBackgroundBrush(RequestStatus status) => status switch
        {
            RequestStatus.Pending => WorkspaceSurfaceChrome.BrushFrom("#FFF9EC"),
            RequestStatus.Executed => WorkspaceSurfaceChrome.BrushFrom("#F2FBF4"),
            RequestStatus.Rejected => WorkspaceSurfaceChrome.BrushFrom("#FFF3F3"),
            RequestStatus.Cancelled => WorkspaceSurfaceChrome.BrushFrom("#F2F7FF"),
            RequestStatus.Superseded => WorkspaceSurfaceChrome.BrushFrom("#F2F7FF"),
            _ => WorkspaceSurfaceChrome.BrushFrom("#F8FAFC")
        };

        private static Brush StatusBorderBrush(RequestStatus status) => status switch
        {
            RequestStatus.Pending => WorkspaceSurfaceChrome.BrushFrom("#F6DE99"),
            RequestStatus.Executed => WorkspaceSurfaceChrome.BrushFrom("#C9EFCF"),
            RequestStatus.Rejected => WorkspaceSurfaceChrome.BrushFrom("#F7C5C5"),
            RequestStatus.Cancelled => WorkspaceSurfaceChrome.BrushFrom("#CADCFF"),
            RequestStatus.Superseded => WorkspaceSurfaceChrome.BrushFrom("#CADCFF"),
            _ => WorkspaceSurfaceChrome.BrushFrom("#E2E8F0")
        };
    }

    public sealed record RequestsWorkspaceMetrics(
        string Total,
        string Pending,
        string MissingResponse,
        string Closed);

    public sealed record RequestsWorkspaceFilterResult(
        IReadOnlyList<RequestListDisplayItem> Items);

    public sealed record RequestsWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BankText,
        ImageSource? BankLogo,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string LetterAttachmentTitle,
        string LetterAttachmentMeta,
        string ResponseAttachmentTitle,
        string ResponseAttachmentMeta,
        string ResponseAttachHint,
        bool CanOpenGuarantee,
        bool CanOpenLetter,
        bool CanOpenResponse,
        bool CanUseResponseAttachAction,
        string ResponseAttachActionLabel,
        string ResponseAttachActionHint,
        bool CanRunPrimaryAction,
        string PrimaryActionLabel,
        string PrimaryActionHint);
}
