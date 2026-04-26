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

            string summary = items.Count == 0
                ? "لا توجد طلبات مطابقة."
                : $"عرض 1 - {items.Count.ToString("N0", CultureInfo.InvariantCulture)} من أصل {allRequests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب";

            return new RequestsWorkspaceFilterResult(items, summary);
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
                    "---",
                    "---",
                    "---",
                    "---",
                    "---",
                    "---",
                    false,
                    false,
                    false,
                    false,
                    "رد البنك");
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
                $"{item.ReferenceTypeLabel}: {item.ReferenceNumber}",
                item.CurrentVersionLabel,
                $"{item.CurrentValueFieldLabel}: {item.CurrentValueDisplay}",
                $"{item.RequestedValueFieldLabel}: {item.RequestedValueDisplay}",
                $"تاريخ الطلب: {request.RequestDate:yyyy/MM/dd} | تاريخ الرد: {item.ResponseDateLabel}",
                string.IsNullOrWhiteSpace(request.Notes) ? "لا توجد ملاحظات." : request.Notes,
                string.IsNullOrWhiteSpace(request.ResponseNotes) ? "لا يوجد رد مسجل." : request.ResponseNotes,
                true,
                request.Status == RequestStatus.Pending,
                request.HasLetter,
                request.HasResponseDocument || (request.Status != RequestStatus.Pending && !request.HasResponseDocument),
                request.HasResponseDocument ? "فتح الرد" : request.Status == RequestStatus.Pending ? "رد البنك" : "إلحاق الرد");
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
        IReadOnlyList<RequestListDisplayItem> Items,
        string Summary);

    public sealed record RequestsWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BankText,
        ImageSource? BankLogo,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string Reference,
        string Status,
        string Current,
        string Requested,
        string Dates,
        string Notes,
        string Response,
        bool CanOpenGuarantee,
        bool CanRegisterResponse,
        bool CanOpenLetter,
        bool CanOpenResponse,
        string ResponseActionLabel);
}
