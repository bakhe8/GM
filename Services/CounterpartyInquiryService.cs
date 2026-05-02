using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class CounterpartyInquiryService
    {
        private readonly IDatabaseService _databaseService;

        public CounterpartyInquiryService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public OperationalInquiryResult GetPendingRequestsForBank(string bank)
        {
            List<WorkflowRequestListItem> pendingRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    Bank = bank,
                    RequestStatus = RequestStatus.Pending,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
                });

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"bank-pending:{bank.Trim().ToUpperInvariant()}",
                Title = "ما الطلبات المعلقة لدى هذا البنك؟",
                Subject = $"البنك: {bank}"
            };

            if (!pendingRequests.Any())
            {
                result.Answer = $"لا توجد طلبات معلقة محفوظة حاليًا لدى البنك {bank}.";
                result.Explanation = "لم يتم العثور على طلبات قيد الانتظار مطابقة لهذا البنك في مركز العمليات.";
                result.EventDate = DateTime.Now;
                result.Facts.Add(new OperationalInquiryFact { Label = "عدد الطلبات المعلقة", Value = "0" });
                return result;
            }

            WorkflowRequestListItem oldestPending = pendingRequests.First();
            result.EventDate = oldestPending.Request.RequestDate;
            result.Answer =
                $"يوجد {pendingRequests.Count} طلب/طلبات معلقة لدى البنك {bank}. أقدمها يعود إلى {DualCalendarDateService.FormatDate(oldestPending.Request.RequestDate, oldestPending.CurrentDateCalendar)} على الضمان رقم {oldestPending.GuaranteeNo}.";
            result.Explanation = "تم احتساب جميع الطلبات التي حالتها الحالية قيد الانتظار والمطابقة للبنك المحدد.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الطلبات المعلقة", Value = pendingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم طلب", Value = DualCalendarDateService.FormatDate(oldestPending.Request.RequestDate, oldestPending.CurrentDateCalendar) });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم رقم ضمان", Value = oldestPending.GuaranteeNo });
            result.Facts.Add(new OperationalInquiryFact { Label = "تمديدات معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Extension).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تخفيضات معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Reduction).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إفراجات معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Release).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "طلبات تسييل معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Liquidation).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تحققات معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Verification).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "استبدالات معلقة", Value = pendingRequests.Count(item => item.Request.Type == RequestType.Replacement).ToString("N0") });

            foreach (WorkflowRequestListItem item in pendingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"{item.Request.TypeLabel} - {item.GuaranteeNo}",
                    Details = $"{item.Supplier} | مفتوح منذ {(DateTime.Now.Date - item.Request.RequestDate.Date).Days} يوم/أيام"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetBankConfirmationSummary(string bank)
        {
            List<WorkflowRequestListItem> bankRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    Bank = bank,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
                });

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"bank-confirmation:{bank.Trim().ToUpperInvariant()}",
                Title = "هل أكد هذا البنك كل طلباتنا؟",
                Subject = $"البنك: {bank}"
            };

            if (!bankRequests.Any())
            {
                result.Answer = $"لا توجد طلبات محفوظة لهذا البنك حتى الآن.";
                result.Explanation = "لذلك لا يمكن الحديث عن تأكيد أو عدم تأكيد الطلبات لهذا البنك.";
                result.EventDate = DateTime.Now;
                return result;
            }

            int pendingCount = bankRequests.Count(item => item.Request.Status == RequestStatus.Pending);
            int executedCount = bankRequests.Count(item => item.Request.Status == RequestStatus.Executed);
            int rejectedCount = bankRequests.Count(item => item.Request.Status == RequestStatus.Rejected);
            int cancelledCount = bankRequests.Count(item => item.Request.Status == RequestStatus.Cancelled);
            int supersededCount = bankRequests.Count(item => item.Request.Status == RequestStatus.Superseded);

            result.EventDate = bankRequests.First().Request.ResponseRecordedAt ?? bankRequests.First().Request.RequestDate;
            result.Answer = pendingCount switch
            {
                0 when rejectedCount == 0 && cancelledCount == 0 && supersededCount == 0 =>
                    $"نعم، تم تسجيل استجابة على جميع طلبات البنك {bank}، وجميعها انتهت بنتيجة تنفيذ.",
                0 =>
                    $"تم تسجيل استجابة على جميع طلبات البنك {bank}، لكن ليست كلها تنفيذًا؛ توجد طلبات مرفوضة أو مغلقة دون تنفيذ.",
                _ =>
                    $"لا، ما زال لدى البنك {bank} عدد {pendingCount} طلب/طلبات قيد الانتظار ولم تُسجل استجابة عليها بعد."
            };

            result.Explanation =
                $"إجمالي الطلبات: {bankRequests.Count}. منفذ: {executedCount}، مرفوض: {rejectedCount}، مُلغى: {cancelledCount}، مُسقط آليًا: {supersededCount}، قيد الانتظار: {pendingCount}.";

            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي الطلبات", Value = bankRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "منفذ", Value = executedCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "قيد الانتظار", Value = pendingCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "مرفوض", Value = rejectedCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "مُلغى", Value = cancelledCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "مُسقط آليًا", Value = supersededCount.ToString("N0") });

            foreach (WorkflowRequestListItem item in bankRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"{item.Request.TypeLabel} - {item.GuaranteeNo}",
                    Details = item.Request.Status == RequestStatus.Pending
                        ? $"قيد الانتظار منذ {DualCalendarDateService.FormatDate(item.Request.RequestDate, item.CurrentDateCalendar)}"
                        : $"{item.Request.StatusLabel} بتاريخ {DualCalendarDateService.FormatDate(item.Request.ResponseRecordedAt ?? item.Request.RequestDate, item.CurrentDateCalendar)}"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetLatestActivityForSupplier(string supplier)
        {
            List<Guarantee> supplierGuarantees = _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    Supplier = supplier,
                    SortMode = GuaranteeQuerySortMode.CreatedAtDescending
                });

            List<WorkflowRequestListItem> supplierRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    Supplier = supplier,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
                });

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"supplier-latest:{supplier.Trim().ToUpperInvariant()}",
                Title = "ما آخر ما حدث لضمانات هذا المورد؟",
                Subject = $"المورد: {supplier}"
            };

            if (!supplierGuarantees.Any() && !supplierRequests.Any())
            {
                result.Answer = $"لا توجد ضمانات أو طلبات محفوظة مرتبطة بالمورد {supplier}.";
                result.Explanation = "لم يتم العثور على بيانات مطابقة لاسم المورد المحدد.";
                result.EventDate = DateTime.Now;
                result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات الحالية", Value = "0" });
                return result;
            }

            Guarantee? latestGuarantee = supplierGuarantees.FirstOrDefault();
            WorkflowRequestListItem? latestRequest = supplierRequests.FirstOrDefault();

            DateTime latestGuaranteeDate = latestGuarantee?.CreatedAt ?? DateTime.MinValue;
            DateTime latestRequestDate = latestRequest == null
                ? DateTime.MinValue
                : latestRequest.Request.ResponseRecordedAt ?? latestRequest.Request.RequestDate;

            if (latestRequest != null && latestRequestDate >= latestGuaranteeDate)
            {
                result.EventDate = latestRequestDate;
                result.Answer = latestRequest.Request.Status == RequestStatus.Pending
                    ? $"آخر ما حدث لضمانات المورد {supplier} هو إنشاء {latestRequest.Request.TypeLabel} على الضمان رقم {latestRequest.GuaranteeNo} بتاريخ {DualCalendarDateService.FormatDate(latestRequest.Request.RequestDate, latestRequest.CurrentDateCalendar)}، وما زال الطلب قيد الانتظار."
                    : $"آخر ما حدث لضمانات المورد {supplier} هو تسجيل استجابة بنك على {latestRequest.Request.TypeLabel} للضمان رقم {latestRequest.GuaranteeNo} بتاريخ {DualCalendarDateService.FormatDate(latestRequestDate, latestRequest.CurrentDateCalendar)}، وكانت النتيجة {latestRequest.Request.StatusLabel}.";
                result.Explanation = "تم اختيار أحدث حدث بين الطلبات والإصدارات الرسمية الخاصة بالمورد المحدد.";
            }
            else if (latestGuarantee != null)
            {
                result.EventDate = latestGuarantee.CreatedAt;
                result.Answer =
                    $"آخر ما حدث لضمانات المورد {supplier} هو تحديث السجل الرسمي للضمان رقم {latestGuarantee.GuaranteeNo} إلى الإصدار {latestGuarantee.VersionLabel} بتاريخ {DualCalendarDateService.FormatDateTime(latestGuarantee.CreatedAt, latestGuarantee.DateCalendar)}.";
                result.Explanation = "لم يظهر طلب أحدث من آخر تحديث رسمي محفوظ لهذا المورد.";
            }

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات الحالية", Value = supplierGuarantees.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "طلبات معلقة", Value = supplierRequests.Count(item => item.Request.Status == RequestStatus.Pending).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "ضمانات قريبة الانتهاء", Value = supplierGuarantees.Count(g => g.IsExpiringSoon).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "ضمانات منتهية وتحتاج إفراج", Value = supplierGuarantees.Count(g => g.NeedsExpiryFollowUp).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "آخر طلب",
                Value = latestRequest == null
                    ? "---"
                    : $"{latestRequest.Request.TypeLabel} - {latestRequest.Request.StatusLabel} - {latestRequest.GuaranteeNo}"
            });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "أحدث ضمان محدث",
                Value = latestGuarantee == null
                    ? "---"
                    : $"{latestGuarantee.GuaranteeNo} - {latestGuarantee.VersionLabel}"
            });

            foreach (Guarantee guarantee in supplierGuarantees.Take(5))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = guarantee.CreatedAt,
                    DateCalendar = guarantee.DateCalendar,
                    Title = $"تحديث سجل رسمي - {guarantee.GuaranteeNo}",
                    Details = $"{guarantee.VersionLabel} | {guarantee.LifecycleStatusLabel} | الانتهاء {DualCalendarDateService.FormatDate(guarantee.ExpiryDate, guarantee.DateCalendar)}"
                });
            }

            foreach (WorkflowRequestListItem item in supplierRequests.Take(5))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"{item.Request.TypeLabel} - {item.GuaranteeNo}",
                    Details = item.Request.Status == RequestStatus.Pending
                        ? $"قيد الانتظار منذ {DualCalendarDateService.FormatDate(item.Request.RequestDate, item.CurrentDateCalendar)}"
                        : $"{item.Request.StatusLabel} بتاريخ {DualCalendarDateService.FormatDate(item.Request.ResponseRecordedAt ?? item.Request.RequestDate, item.CurrentDateCalendar)}"
                });
            }

            result.Timeline.Sort((left, right) => right.Timestamp.CompareTo(left.Timestamp));
            if (result.Timeline.Count > 10)
            {
                result.Timeline.RemoveRange(10, result.Timeline.Count - 10);
            }

            return result;
        }
    }
}
