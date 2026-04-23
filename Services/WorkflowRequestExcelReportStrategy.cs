using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowRequestExcelReportStrategy : IWorkflowRequestExcelReportStrategy
    {
        public ExcelExportResult ExportWorkflowRequestsByStatus(RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            string statusLabel = ExcelReportSupport.GetRequestStatusLabel(status);
            var filteredRequests = requests
                .Where(item => item.Request.Status == status)
                .OrderBy(item => item.GuaranteeNo)
                .ThenBy(item => item.Request.SequenceNumber)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Status_{ExcelReportSupport.MakeSafeFileName(statusLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات حسب الحالة",
                $"الطلبات حسب الحالة: {statusLabel}",
                $"يعرض جميع الطلبات المطابقة للحالة المحددة. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            string typeLabel = ExcelReportSupport.GetRequestTypeLabel(type);
            var filteredRequests = requests
                .Where(item => item.Request.Type == type)
                .OrderBy(item => item.GuaranteeNo)
                .ThenBy(item => item.Request.SequenceNumber)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Type_{ExcelReportSupport.MakeSafeFileName(typeLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات حسب النوع",
                $"الطلبات حسب النوع: {typeLabel}",
                $"يعرض جميع الطلبات المطابقة لنوع الطلب المحدد. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportWorkflowRequestsByTypeAndStatus(RequestType type, RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            string typeLabel = ExcelReportSupport.GetRequestTypeLabel(type);
            string statusLabel = ExcelReportSupport.GetRequestStatusLabel(status);
            var filteredRequests = requests
                .Where(item => item.Request.Type == type && item.Request.Status == status)
                .OrderBy(item => item.GuaranteeNo)
                .ThenBy(item => item.Request.SequenceNumber)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_{ExcelReportSupport.MakeSafeFileName(typeLabel)}_{ExcelReportSupport.MakeSafeFileName(statusLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات حسب النوع والحالة",
                $"الطلبات حسب النوع والحالة: {typeLabel} / {statusLabel}",
                $"يعرض الطلبات المطابقة للنوع والحالة المحددين. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportExecutedRequestsWithEffect(IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Status == RequestStatus.Executed)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Executed_With_Effect_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات المنفذة",
                "الطلبات المنفذة مع أثرها",
                $"يعرض الطلبات المنفذة مع أثرها على السجل أو نتيجة التنفيذ. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportClosedWithoutExecutionRequests(IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Status is RequestStatus.Rejected or RequestStatus.Cancelled or RequestStatus.Superseded)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Closed_Without_Execution_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات المغلقة بدون تنفيذ",
                "الطلبات المغلقة بدون تنفيذ",
                $"يعرض الطلبات التي أغلقت دون إنشاء أثر تنفيذي فعلي. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportSupersededRequests(IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Status == RequestStatus.Superseded)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Superseded_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات المُسقطة آليًا",
                "الطلبات المُسقطة آليًا",
                $"يعرض الطلبات التي أغلقها النظام تلقائيًا بسبب تغير المسار أو تنفيذ طلب آخر. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportRequestsWithoutResponseDocument(IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Status == RequestStatus.Executed && !item.Request.HasResponseDocument)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Executed_Without_Response_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات بلا مستند رد بنك",
                "الطلبات المنفذة بلا مستند رد بنك",
                $"يعرض الطلبات المنفذة التي لم يُحفظ لها مستند رد بنك. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportPendingWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            string typeLabel = ExcelReportSupport.GetRequestTypeLabel(type).Replace("طلب ", string.Empty);
            var pendingRequests = requests
                .Where(item => item.Request.Status == RequestStatus.Pending && item.Request.Type == type)
                .OrderBy(item => item.GuaranteeNo)
                .ThenBy(item => item.Request.SequenceNumber)
                .ToList();

            return ExportWorkflowRequestsReport(
                pendingRequests,
                $"Pending_{ExcelReportSupport.MakeSafeFileName(typeLabel)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات المعلقة",
                $"الطلبات المعلقة - {typeLabel}",
                $"يعرض جميع الطلبات المعلقة من هذا النوع. عدد الطلبات: {pendingRequests.Count}");
        }

        public ExcelExportResult ExportPendingRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var pendingRequests = requests
                .Where(item => item.Request.Status == RequestStatus.Pending &&
                               string.Equals(item.Bank, bank, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Request.RequestDate)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                pendingRequests,
                $"Pending_Bank_{ExcelReportSupport.MakeSafeFileName(bank)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير الطلبات المعلقة حسب البنك",
                $"الطلبات المعلقة - البنك: {bank}",
                $"يعرض جميع الطلبات المعلقة لدى البنك المحدد. عدد الطلبات: {pendingRequests.Count}");
        }

        public ExcelExportResult ExportWorkflowRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            var bankRequests = requests
                .Where(item => string.Equals(item.Bank, bank, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Request.ResponseRecordedAt ?? item.Request.RequestDate)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                bankRequests,
                $"Workflow_Bank_{ExcelReportSupport.MakeSafeFileName(bank)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير طلبات البنك",
                $"طلبات البنك: {bank}",
                $"يعرض جميع الطلبات المرتبطة بالبنك المحدد. عدد الطلبات: {bankRequests.Count}");
        }

        public ExcelExportResult ExportOldestPendingRequests(IReadOnlyList<WorkflowRequestListItem> requests, int topCount = 10)
        {
            var oldestPending = requests
                .Where(item => item.Request.Status == RequestStatus.Pending)
                .OrderBy(item => item.Request.RequestDate)
                .ThenBy(item => item.GuaranteeNo)
                .Take(Math.Max(1, topCount))
                .ToList();

            return ExportWorkflowRequestsReport(
                oldestPending,
                $"Workflow_Oldest_Pending_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير أقدم الطلبات المعلقة",
                $"أقدم {oldestPending.Count} طلبات معلقة",
                "يعرض أقدم الطلبات المفتوحة التي لم يسجل لها رد بنك بعد.");
        }

        public ExcelExportResult ExportExecutedExtensionsThisMonth(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Type == RequestType.Extension &&
                               item.Request.Status == RequestStatus.Executed &&
                               item.Request.ResponseRecordedAt.HasValue &&
                               item.Request.ResponseRecordedAt.Value >= periodStart &&
                               item.Request.ResponseRecordedAt.Value <= periodEnd)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Extensions_{periodStart:yyyyMM}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "حفظ تقرير التمديدات المنفذة هذا الشهر",
                "التمديدات المنفذة هذا الشهر",
                $"يعرض طلبات التمديد المنفذة خلال الفترة من {periodStart:yyyy-MM-dd} إلى {periodEnd:yyyy-MM-dd}. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportContractRelatedReleasedInPeriod(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd)
        {
            var filteredRequests = requests
                .Where(item => item.Request.Type == RequestType.Release &&
                               item.Request.Status == RequestStatus.Executed &&
                               item.IsContractRelated &&
                               item.Request.ResponseRecordedAt.HasValue &&
                               item.Request.ResponseRecordedAt.Value.Date >= periodStart.Date &&
                               item.Request.ResponseRecordedAt.Value.Date <= periodEnd.Date)
                .OrderByDescending(item => item.Request.ResponseRecordedAt)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Contract_Releases_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx",
                "حفظ تقرير الإفراجات المرتبطة بالعقود",
                "الإفراجات المرتبطة بالعقود",
                $"يعرض طلبات الإفراج المنفذة المرتبطة بالعقود خلال الفترة من {periodStart:yyyy-MM-dd} إلى {periodEnd:yyyy-MM-dd}. عدد الطلبات: {filteredRequests.Count}");
        }

        public ExcelExportResult ExportEmployeeContractRequestsInPeriod(string employeeName, IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd)
        {
            var filteredRequests = requests
                .Where(item =>
                    item.IsContractRelated &&
                    (item.Request.Type == RequestType.Extension || item.Request.Type == RequestType.Release) &&
                    string.Equals(item.Request.CreatedBy?.Trim(), employeeName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    item.Request.RequestDate.Date >= periodStart.Date &&
                    item.Request.RequestDate.Date <= periodEnd.Date)
                .OrderByDescending(item => item.Request.RequestDate)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            return ExportWorkflowRequestsReport(
                filteredRequests,
                $"Workflow_Employee_{ExcelReportSupport.MakeSafeFileName(employeeName)}_{periodStart:yyyyMM}.xlsx",
                "حفظ تقرير طلبات الموظف",
                $"طلبات الموظف: {employeeName}",
                $"يعرض طلبات التمديد والإفراج المتعلقة بالعقود والمنشأة خلال الفترة من {periodStart:yyyy-MM-dd} إلى {periodEnd:yyyy-MM-dd}. عدد الطلبات: {filteredRequests.Count}");
        }

        public bool ExportWorkflowRequestsReportToPath(
            IReadOnlyList<WorkflowRequestListItem> requests,
            string reportTitle,
            string reportSubtitle,
            string outputPath)
        {
            if (requests.Count == 0)
            {
                return false;
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("الطلبات");
            worksheet.SetRightToLeft(true);

            ExcelReportSupport.WriteWorkflowRequestsWorksheet(worksheet, requests, reportTitle, reportSubtitle);
            return ExcelReportSupport.SaveWorkbook(workbook, outputPath).Exported;
        }

        private ExcelExportResult ExportWorkflowRequestsReport(
            IReadOnlyList<WorkflowRequestListItem> requests,
            string defaultFileName,
            string saveDialogTitle,
            string reportTitle,
            string reportSubtitle)
        {
            try
            {
                if (requests.Count == 0)
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(defaultFileName, saveDialogTitle);
                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                return ExportWorkflowRequestsReportToPath(requests, reportTitle, reportSubtitle, saveFileDialog.FileName)
                    ? ExcelExportResult.Saved(saveFileDialog.FileName)
                    : ExcelExportResult.Cancelled;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportWorkflowRequestsReport",
                    "تعذر تصدير تقرير الطلبات إلى Excel.");
            }
        }
    }
}
