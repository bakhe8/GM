using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Utils
{
    public static class WorkspaceReportCatalog
    {
        public static IReadOnlyList<WorkspaceReportAction> PortfolioActions { get; } =
        [
            new("portfolio.all", "المحفظة الكاملة", "تصدير قائمة الضمانات كما هي الآن."),
            new("portfolio.summary", "ملخص المحفظة", "مخرج تنفيذي يجمع الضمانات والطلبات."),
            new("portfolio.expiring", "قريب الانتهاء", "الضمانات التي تقترب من نهاية المدة."),
            new("portfolio.expired", "منتهية وتحتاج متابعة", "الضمانات المنتهية زمنيًا التي ما زالت بحاجة إلى إجراء تشغيلي."),
            new("portfolio.no-attachments", "الضمانات بلا مرفقات", "السجلات الحالية التي لا تحتوي على أي مرفقات."),
            new("portfolio.versions.no-attachments", "الإصدارات بلا مرفقات", "كل الإصدارات في السجل الكامل التي لا تحتوي على مرفقات.")
        ];

        public static IReadOnlyList<WorkspaceReportAction> OperationalActions { get; } =
        [
            new("requests.pending", "الطلبات المعلقة", "كل الطلبات التي ما زالت بانتظار المعالجة أو الرد."),
            new("daily.followup", "متابعة يومية", "مخرج متابعة يومية يجمع أهم الضمانات والطلبات."),
            new("stats.bank", "إحصائية حسب البنك", "تجميع المحفظة حسب البنوك."),
            new("stats.supplier", "إحصائية حسب المورد", "تجميع المحفظة حسب الموردين.")
        ];

        public static bool Run(
            string reportKey,
            IDatabaseService databaseService,
            IExcelService excelService)
        {
            return reportKey switch
            {
                "portfolio.all" => excelService.ExportGuarantees(QueryAllGuarantees(databaseService)),
                "portfolio.summary" => excelService.ExportGuaranteePortfolioSummary(
                    QueryAllGuarantees(databaseService),
                    QueryAllRequests(databaseService)),
                "portfolio.expiring" => excelService.ExportExpiringSoonGuarantees(QueryExpiringSoonGuarantees(databaseService)),
                "portfolio.expired" => excelService.ExportExpiredActiveGuarantees(QueryExpiredFollowUpGuarantees(databaseService)),
                "portfolio.no-attachments" => excelService.ExportGuaranteesWithoutAttachments(QueryCurrentGuaranteesWithoutAttachments(databaseService)),
                "portfolio.versions.no-attachments" => excelService.ExportGuaranteeVersionsWithoutAttachments(QueryGuaranteeVersionsWithoutAttachments(databaseService)),
                "requests.pending" => excelService.ExportWorkflowRequestsByStatus(
                    RequestStatus.Pending,
                    QueryPendingRequests(databaseService)),
                "daily.followup" => excelService.ExportDailyFollowUpReport(
                    QueryDailyFollowUpGuarantees(databaseService),
                    QueryDailyFollowUpRequests(databaseService)),
                "stats.bank" => excelService.ExportGuaranteeStatisticsByBank(QueryAllGuarantees(databaseService)),
                "stats.supplier" => excelService.ExportGuaranteeStatisticsBySupplier(QueryAllGuarantees(databaseService)),
                _ => false
            };
        }

        private static List<Guarantee> QueryAllGuarantees(IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions());
        }

        private static List<Guarantee> QueryExpiringSoonGuarantees(IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                TimeStatus = GuaranteeTimeStatus.ExpiringSoon,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        private static List<Guarantee> QueryExpiredFollowUpGuarantees(IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions
                {
                    TimeStatus = GuaranteeTimeStatus.Expired,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                })
                .Where(guarantee => guarantee.NeedsExpiryFollowUp)
                .ToList();
        }

        private static List<Guarantee> QueryDailyFollowUpGuarantees(IDatabaseService databaseService)
        {
            List<Guarantee> expiringSoon = QueryExpiringSoonGuarantees(databaseService);
            List<Guarantee> expiredActive = QueryExpiredFollowUpGuarantees(databaseService);

            return [.. expiringSoon, .. expiredActive];
        }

        private static List<WorkflowRequestListItem> QueryAllRequests(IDatabaseService databaseService)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions());
        }

        private static List<Guarantee> QueryAllGuaranteeVersions(IDatabaseService databaseService)
        {
            List<Guarantee> currentGuarantees = QueryAllGuarantees(databaseService);

            return currentGuarantees
                .SelectMany(guarantee => databaseService.GetGuaranteeHistory(guarantee.Id))
                .GroupBy(guarantee => guarantee.Id)
                .Select(group => group.First())
                .ToList();
        }

        private static List<Guarantee> QueryCurrentGuaranteesWithoutAttachments(IDatabaseService databaseService)
        {
            return QueryAllGuarantees(databaseService)
                .Where(guarantee => guarantee.AttachmentCount == 0)
                .ToList();
        }

        private static List<Guarantee> QueryGuaranteeVersionsWithoutAttachments(IDatabaseService databaseService)
        {
            return QueryAllGuaranteeVersions(databaseService)
                .Where(guarantee => guarantee.AttachmentCount == 0)
                .ToList();
        }

        private static List<WorkflowRequestListItem> QueryPendingRequests(IDatabaseService databaseService)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });
        }

        private static List<WorkflowRequestListItem> QueryDailyFollowUpRequests(IDatabaseService databaseService)
        {
            List<WorkflowRequestListItem> pendingRequests = QueryPendingRequests(databaseService);
            List<WorkflowRequestListItem> executedWithoutResponse = databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Executed,
                PendingOrMissingResponseOnly = true,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });

            return [.. pendingRequests, .. executedWithoutResponse];
        }

        public sealed record WorkspaceReportAction(string Key, string Title, string Description);
    }
}
