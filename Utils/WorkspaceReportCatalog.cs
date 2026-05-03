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
            new("portfolio.expired", "منتهية وتحتاج إفراج", "الضمانات المنتهية زمنيًا التي ما زالت بحاجة إلى إفراج/إعادة للبنك."),
            new("portfolio.no-attachments", "الضمانات بلا مرفقات", "السجلات الحالية التي لا تحتوي على أي مرفقات."),
            new("portfolio.versions.no-attachments", "الإصدارات بلا مرفقات", "كل الإصدارات في السجل الكامل التي لا تحتوي على مرفقات.")
        ];

        public static IReadOnlyList<WorkspaceReportAction> OperationalActions { get; } =
        [
            new("requests.pending", "الطلبات المعلقة", "كل الطلبات التي ما زالت بانتظار المعالجة أو الرد."),
            new("requests.pending.extension", "طلبات التمديد المعلقة", "تصدير جميع طلبات التمديد التي ما زالت بانتظار رد البنك."),
            new("requests.pending.reduction", "طلبات التخفيض المعلقة", "تصدير جميع طلبات التخفيض التي ما زالت بانتظار رد البنك."),
            new("requests.pending.release", "طلبات الإفراج المعلقة", "تصدير جميع طلبات الإفراج التي ما زالت بانتظار رد البنك."),
            new("requests.pending.liquidation", "طلبات التسييل المعلقة", "تصدير جميع طلبات التسييل التي ما زالت بانتظار رد البنك."),
            new("requests.pending.verification", "طلبات التحقق المعلقة", "تصدير جميع طلبات التحقق التي ما زالت بانتظار رد البنك."),
            new("requests.pending.replacement", "طلبات الاستبدال المعلقة", "تصدير جميع طلبات الاستبدال التي ما زالت بانتظار رد البنك."),
            new("operational.guarantee-file", "تقرير ضمان محدد", "تقرير رسمي لبيانات ضمان واحد عبر رقم الضمان."),
            new("operational.guarantee-history", "سجل ضمان محدد", "تصدير سجل ضمان محدد يشمل الإصدارات والطلبات المرتبطة."),
            new("operational.guarantee-history-print", "طباعة سجل ضمان محدد", "طباعة سجل ضمان محدد يشمل الإصدارات والطلبات المرتبطة."),
            new("operational.bank-pending-requests", "طلبات بنك معلقة", "تقرير يعرض الطلبات المعلقة لدى بنك محدد."),
            new("operational.bank-requests", "طلبات بنك كاملة", "تقرير يعرض كل طلبات بنك محدد بجميع حالاتها."),
            new("operational.supplier-guarantees", "ضمانات مورد محدد", "تقرير يعرض الضمانات الحالية الخاصة بمورد محدد."),
            new("operational.oldest-pending", "أقدم الطلبات المعلقة", "تقرير رسمي يعرض أقدم الطلبات المفتوحة التي لم يسجل لها رد بنك."),
            new("operational.executed-extensions-this-month", "التمديدات المنفذة هذا الشهر", "تقرير تشغيلي يعرض طلبات التمديد المنفذة خلال الشهر الحالي."),
            new("operational.active-purchase-order-only", "ضمانات أوامر الشراء السارية", "تقرير يعرض الضمانات السارية المرتبطة بأوامر الشراء فقط."),
            new("operational.contract-released-last-week", "إفراجات العقود خلال آخر أسبوع", "تقرير يعرض الإفراجات المنفذة للضمانات المرتبطة بالعقود خلال آخر أسبوع مكتمل."),
            new("operational.employee-contract-requests-last-month", "طلبات موظف للعقود", "تقرير يطلب اسم الموظف ثم يعرض طلبات التمديد والإفراج التي أنشأها للعقود خلال الشهر الماضي."),
            new("operational.expired-po-without-extension", "أوامر شراء منتهية تحتاج إفراج", "تقرير يعرض ضمانات أوامر الشراء المنتهية التي تحتاج إفراجًا أو إعادة للبنك."),
            new("daily.followup", "متابعة يومية", "مخرج متابعة يومية يجمع أهم الضمانات والطلبات."),
            new("stats.bank", "إحصائية حسب البنك", "تجميع المحفظة حسب البنوك."),
            new("stats.supplier", "إحصائية حسب المورد", "تجميع المحفظة حسب الموردين.")
        ];

        public static bool Run(
            string reportKey,
            IDatabaseService databaseService,
            IExcelService excelService,
            string? input = null,
            IGuaranteeHistoryDocumentService? historyDocuments = null)
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
                "requests.pending.extension" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Extension,
                    QueryPendingRequestsByType(databaseService, RequestType.Extension)),
                "requests.pending.reduction" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Reduction,
                    QueryPendingRequestsByType(databaseService, RequestType.Reduction)),
                "requests.pending.release" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Release,
                    QueryPendingRequestsByType(databaseService, RequestType.Release)),
                "requests.pending.liquidation" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Liquidation,
                    QueryPendingRequestsByType(databaseService, RequestType.Liquidation)),
                "requests.pending.verification" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Verification,
                    QueryPendingRequestsByType(databaseService, RequestType.Verification)),
                "requests.pending.replacement" => excelService.ExportPendingWorkflowRequestsByType(
                    RequestType.Replacement,
                    QueryPendingRequestsByType(databaseService, RequestType.Replacement)),
                "operational.guarantee-file" when !string.IsNullOrWhiteSpace(input)
                    => TryExportSingleGuaranteeReport(input.Trim(), databaseService, excelService),
                "operational.guarantee-history" when !string.IsNullOrWhiteSpace(input)
                    => TryExportGuaranteeHistory(input.Trim(), databaseService, historyDocuments),
                "operational.guarantee-history-print" when !string.IsNullOrWhiteSpace(input)
                    => TryPrintGuaranteeHistory(input.Trim(), databaseService, historyDocuments),
                "operational.bank-pending-requests" when !string.IsNullOrWhiteSpace(input)
                    => excelService.ExportPendingRequestsByBank(
                        input.Trim(),
                        QueryPendingRequestsByBank(input.Trim(), databaseService)),
                "operational.bank-requests" when !string.IsNullOrWhiteSpace(input)
                    => excelService.ExportWorkflowRequestsByBank(
                        input.Trim(),
                        QueryRequestsByBank(input.Trim(), databaseService)),
                "operational.supplier-guarantees" when !string.IsNullOrWhiteSpace(input)
                    => excelService.ExportGuaranteesBySupplier(
                        input.Trim(),
                        QueryGuaranteesBySupplier(input.Trim(), databaseService)),
                "operational.oldest-pending" => excelService.ExportOldestPendingRequests(
                    QueryOldestPendingRequests(databaseService, 10),
                    topCount: 10),
                "operational.executed-extensions-this-month" => excelService.ExportExecutedExtensionsThisMonth(
                    QueryExecutedExtensions(databaseService, StartOfCurrentMonth(), System.DateTime.Now),
                    StartOfCurrentMonth(),
                    System.DateTime.Now),
                "operational.active-purchase-order-only" => excelService.ExportActivePurchaseOrderOnlyGuarantees(
                    QueryAllGuarantees(databaseService)),
                "operational.contract-released-last-week" => excelService.ExportContractRelatedReleasedInPeriod(
                    QueryContractRelatedReleased(databaseService, StartOfPreviousSevenDayWindow(), EndOfPreviousSevenDayWindow()),
                    StartOfPreviousSevenDayWindow(),
                    EndOfPreviousSevenDayWindow()),
                "operational.employee-contract-requests-last-month" when !string.IsNullOrWhiteSpace(input)
                    => excelService.ExportEmployeeContractRequestsInPeriod(
                        input.Trim(),
                        QueryEmployeeContractRequests(input.Trim(), databaseService, StartOfPreviousMonth(), EndOfPreviousMonth()),
                        StartOfPreviousMonth(),
                        EndOfPreviousMonth()),
                "operational.expired-po-without-extension" => excelService.ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(
                    QueryExpiredPurchaseOrderOnlyWithoutExecutedExtension(databaseService)),
                "daily.followup" => excelService.ExportDailyFollowUpReport(
                    QueryDailyFollowUpGuarantees(databaseService),
                    QueryDailyFollowUpRequests(databaseService)),
                "stats.bank" => excelService.ExportGuaranteeStatisticsByBank(QueryAllGuarantees(databaseService)),
                "stats.supplier" => excelService.ExportGuaranteeStatisticsBySupplier(QueryAllGuarantees(databaseService)),
                _ => false
            };
        }

        public static bool RequiresInput(string reportKey)
        {
            return reportKey is "operational.guarantee-file"
                or "operational.guarantee-history"
                or "operational.guarantee-history-print"
                or "operational.bank-pending-requests"
                or "operational.bank-requests"
                or "operational.supplier-guarantees"
                or "operational.employee-contract-requests-last-month";
        }

        public static string GetInputLabel(string reportKey)
        {
            return reportKey switch
            {
                "operational.guarantee-file"
                    or "operational.guarantee-history"
                    or "operational.guarantee-history-print" => "رقم الضمان",
                "operational.bank-pending-requests" or "operational.bank-requests" => "اسم البنك",
                "operational.supplier-guarantees" => "اسم المورد",
                "operational.employee-contract-requests-last-month" => "اسم الموظف",
                _ => "القيمة المطلوبة"
            };
        }

        public static string GetInputPrompt(string reportKey)
        {
            return reportKey switch
            {
                "operational.guarantee-file" => "أدخل رقم الضمان لإصدار تقرير رسمي لهذا الضمان.",
                "operational.guarantee-history" => "أدخل رقم الضمان لتصدير سجل الإصدارات والطلبات المرتبطة.",
                "operational.guarantee-history-print" => "أدخل رقم الضمان لطباعة سجل الإصدارات والطلبات المرتبطة.",
                "operational.bank-pending-requests" => "أدخل اسم البنك لإصدار تقرير الطلبات المعلقة لديه.",
                "operational.bank-requests" => "أدخل اسم البنك لإصدار تقرير كامل بطلباته.",
                "operational.supplier-guarantees" => "أدخل اسم المورد لإصدار تقرير ضماناته الحالية.",
                "operational.employee-contract-requests-last-month" => "أدخل اسم الموظف لإصدار تقرير طلبات التمديد والإفراج الخاصة بالعقود خلال الشهر الماضي.",
                _ => string.Empty
            };
        }

        public static bool IsPrintAction(string reportKey)
        {
            return reportKey == "operational.guarantee-history-print";
        }

        public static bool ProducesOutputFile(string reportKey)
        {
            return !IsPrintAction(reportKey);
        }

        private static List<Guarantee> QueryAllGuarantees(IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions());
        }

        private static bool TryExportSingleGuaranteeReport(
            string guaranteeNo,
            IDatabaseService databaseService,
            IExcelService excelService)
        {
            Guarantee? guarantee = databaseService.GetCurrentGuaranteeByNo(guaranteeNo);
            return guarantee != null && excelService.ExportSingleGuaranteeReport(guarantee);
        }

        private static bool TryExportGuaranteeHistory(
            string guaranteeNo,
            IDatabaseService databaseService,
            IGuaranteeHistoryDocumentService? historyDocuments)
        {
            return TryRunGuaranteeHistoryDocument(
                guaranteeNo,
                databaseService,
                historyDocuments,
                (documents, guarantee, history, requests) => documents.ExportHistoryToExcel(guarantee, history, requests));
        }

        private static bool TryPrintGuaranteeHistory(
            string guaranteeNo,
            IDatabaseService databaseService,
            IGuaranteeHistoryDocumentService? historyDocuments)
        {
            return TryRunGuaranteeHistoryDocument(
                guaranteeNo,
                databaseService,
                historyDocuments,
                (documents, guarantee, history, requests) => documents.PrintHistory(guarantee, history, requests));
        }

        private static bool TryRunGuaranteeHistoryDocument(
            string guaranteeNo,
            IDatabaseService databaseService,
            IGuaranteeHistoryDocumentService? historyDocuments,
            System.Func<IGuaranteeHistoryDocumentService, Guarantee, IReadOnlyList<Guarantee>, IReadOnlyList<WorkflowRequest>, bool> run)
        {
            Guarantee? currentGuarantee = databaseService.GetCurrentGuaranteeByNo(guaranteeNo);
            if (currentGuarantee == null)
            {
                return false;
            }

            int rootId = currentGuarantee.RootId ?? currentGuarantee.Id;
            List<Guarantee> history = databaseService.GetGuaranteeHistory(currentGuarantee.Id);
            List<WorkflowRequest> requests = databaseService.GetWorkflowRequestsByRootId(rootId);
            IGuaranteeHistoryDocumentService documents = historyDocuments ?? new GuaranteeHistoryDocumentService();
            return run(documents, currentGuarantee, history, requests);
        }

        private static List<Guarantee> QueryGuaranteesBySupplier(
            string supplier,
            IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                Supplier = supplier,
                SortMode = GuaranteeQuerySortMode.CreatedAtDescending
            });
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
                NeedsExpiryFollowUpOnly = true,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
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

        private static System.DateTime StartOfCurrentMonth()
        {
            System.DateTime now = System.DateTime.Now;
            return new System.DateTime(now.Year, now.Month, 1);
        }

        private static System.DateTime StartOfPreviousMonth()
        {
            return StartOfCurrentMonth().AddMonths(-1);
        }

        private static System.DateTime EndOfPreviousMonth()
        {
            return StartOfCurrentMonth().AddDays(-1);
        }

        private static System.DateTime StartOfPreviousSevenDayWindow()
        {
            return System.DateTime.Now.Date.AddDays(-7);
        }

        private static System.DateTime EndOfPreviousSevenDayWindow()
        {
            return System.DateTime.Now.Date.AddDays(-1);
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

        private static List<WorkflowRequestListItem> QueryPendingRequestsByType(
            IDatabaseService databaseService,
            RequestType requestType)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Pending,
                RequestType = requestType,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });
        }

        private static List<WorkflowRequestListItem> QueryPendingRequestsByBank(
            string bank,
            IDatabaseService databaseService)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Pending,
                Bank = bank,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });
        }

        private static List<WorkflowRequestListItem> QueryRequestsByBank(
            string bank,
            IDatabaseService databaseService)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                Bank = bank,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });
        }

        private static List<WorkflowRequestListItem> QueryOldestPendingRequests(
            IDatabaseService databaseService,
            int topCount)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending,
                Limit = topCount
            });
        }

        private static List<WorkflowRequestListItem> QueryExecutedExtensions(
            IDatabaseService databaseService,
            System.DateTime periodStart,
            System.DateTime periodEnd)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestType = RequestType.Extension,
                RequestStatus = RequestStatus.Executed,
                ResponseRecordedFrom = periodStart,
                ResponseRecordedTo = periodEnd,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });
        }

        private static List<WorkflowRequestListItem> QueryContractRelatedReleased(
            IDatabaseService databaseService,
            System.DateTime periodStart,
            System.DateTime periodEnd)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestType = RequestType.Release,
                RequestStatus = RequestStatus.Executed,
                ReferenceType = GuaranteeReferenceType.Contract,
                RequireReferenceNumber = true,
                ResponseRecordedFrom = periodStart,
                ResponseRecordedTo = periodEnd,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });
        }

        private static List<WorkflowRequestListItem> QueryEmployeeContractRequests(
            string employeeName,
            IDatabaseService databaseService,
            System.DateTime periodStart,
            System.DateTime periodEnd)
        {
            return databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                CreatedBy = employeeName,
                ReferenceType = GuaranteeReferenceType.Contract,
                RequireReferenceNumber = true,
                RequestDateFrom = periodStart,
                RequestDateTo = periodEnd,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            })
            .Where(item => item.Request.Type is RequestType.Extension or RequestType.Release)
            .ToList();
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

        private static List<Guarantee> QueryExpiredPurchaseOrderOnlyWithoutExecutedExtension(IDatabaseService databaseService)
        {
            return databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                ReferenceType = GuaranteeReferenceType.PurchaseOrder,
                RequireReferenceNumber = true,
                NeedsExpiryFollowUpOnly = true,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        public sealed record WorkspaceReportAction(string Key, string Title, string Description);
    }
}
