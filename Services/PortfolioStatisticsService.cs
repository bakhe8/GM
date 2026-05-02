using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class PortfolioStatisticsService
    {
        private readonly IDatabaseService _databaseService;

        public PortfolioStatisticsService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public OperationalInquiryResult GetTopOldestPendingRequests(int topCount = 10)
        {
            int safeTopCount = Math.Max(1, topCount);
            List<WorkflowRequestListItem> oldestPending = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestStatus = RequestStatus.Pending,
                    Limit = safeTopCount,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
                });

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"oldest-pending:{safeTopCount}",
                Title = $"ما أكثر {safeTopCount} طلبات تأخر ردها؟",
                Subject = "الطلبات المعلقة الأقدم"
            };

            if (!oldestPending.Any())
            {
                result.Answer = "لا توجد طلبات معلقة حاليًا.";
                result.Explanation = "كل الطلبات الموجودة مغلقة أو منفذة أو لا توجد طلبات مسجلة.";
                result.EventDate = DateTime.Now;
                return result;
            }

            WorkflowRequestListItem oldest = oldestPending.First();
            result.EventDate = oldest.Request.RequestDate;
            result.Answer =
                $"أقدم طلب معلق حاليًا هو {oldest.Request.TypeLabel} على الضمان رقم {oldest.GuaranteeNo} منذ {Math.Max(0, (DateTime.Now.Date - oldest.Request.RequestDate.Date).Days)} يوم/أيام، وتم تجهيز قائمة بأقدم {oldestPending.Count} طلبات مفتوحة.";
            result.Explanation = "الترتيب تم تصاعديًا حسب تاريخ الطلب مع استبعاد كل الطلبات غير المعلقة.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد العناصر في القائمة", Value = oldestPending.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم تاريخ طلب", Value = DualCalendarDateService.FormatDate(oldest.Request.RequestDate, oldest.CurrentDateCalendar) });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم بنك", Value = oldest.Bank });
            result.Facts.Add(new OperationalInquiryFact { Label = "تمديدات ضمن القائمة", Value = oldestPending.Count(item => item.Request.Type == RequestType.Extension).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إفراجات ضمن القائمة", Value = oldestPending.Count(item => item.Request.Type == RequestType.Release).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "طلبات تسييل ضمن القائمة", Value = oldestPending.Count(item => item.Request.Type == RequestType.Liquidation).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تخفيضات ضمن القائمة", Value = oldestPending.Count(item => item.Request.Type == RequestType.Reduction).ToString("N0") });

            foreach (WorkflowRequestListItem item in oldestPending)
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"{item.Request.TypeLabel} - {item.GuaranteeNo}",
                    Details = $"{item.Bank} | {item.Supplier} | مفتوح منذ {(DateTime.Now.Date - item.Request.RequestDate.Date).Days} يوم/أيام"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetExecutedExtensionsThisMonth()
        {
            DateTime now = DateTime.Now;
            DateTime startOfMonth = new DateTime(now.Year, now.Month, 1);

            List<WorkflowRequestListItem> matchingRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Extension,
                    RequestStatus = RequestStatus.Executed,
                    ResponseRecordedFrom = startOfMonth,
                    ResponseRecordedTo = now,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
                });

            int uniqueGuarantees = matchingRequests
                .Select(item => item.RootGuaranteeId)
                .Distinct()
                .Count();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"extensions-this-month:{startOfMonth:yyyyMM}",
                Title = "كم ضمان قمنا بتمديده هذا الشهر؟",
                Subject = $"الفترة: {DualCalendarDateService.FormatGregorianDate(startOfMonth)} إلى {DualCalendarDateService.FormatGregorianDate(now)}",
                EventDate = now
            };

            result.Answer = matchingRequests.Any()
                ? $"تم تنفيذ {matchingRequests.Count} طلب/طلبات تمديد هذا الشهر، تمثل {uniqueGuarantees} ضمانًا مختلفًا."
                : "لا توجد طلبات تمديد منفذة مسجلة خلال هذا الشهر حتى الآن.";
            result.Explanation = "الاحتساب تم بناءً على طلبات التمديد التي حالتها منفذ وتاريخ تسجيل استجابة البنك عليها يقع ضمن الشهر الحالي.";

            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي طلبات التمديد المنفذة", Value = matchingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات المختلفة", Value = uniqueGuarantees.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = DualCalendarDateService.FormatGregorianDate(startOfMonth) });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = DualCalendarDateService.FormatGregorianDate(now) });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"تمديد منفذ - {item.GuaranteeNo}",
                    Details = $"{item.Supplier} | الانتهاء الحالي {DualCalendarDateService.FormatDate(item.CurrentExpiryDate, item.CurrentDateCalendar)}"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetActivePurchaseOrderOnlyGuarantees()
        {
            List<Guarantee> matchingGuarantees = _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    LifecycleStatus = GuaranteeLifecycleStatus.Active,
                    ReferenceType = GuaranteeReferenceType.PurchaseOrder,
                    RequireReferenceNumber = true,
                    NotExpiredOnly = true,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                });

            decimal totalAmount = matchingGuarantees.Sum(g => g.Amount);

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = "active-po-only-guarantees",
                Title = "كم لدينا من ضمانات سارية تخص أوامر الشراء فقط؟",
                Subject = "الضمانات السارية - أوامر الشراء",
                EventDate = DateTime.Now
            };

            result.Answer = matchingGuarantees.Any()
                ? $"يوجد {matchingGuarantees.Count} ضمانًا/ضمانات سارية تخص أوامر الشراء فقط، بإجمالي مبالغ {ArabicAmountFormatter.FormatSaudiRiyals(totalAmount)}."
                : "لا توجد حاليًا ضمانات سارية تخص أوامر الشراء فقط.";
            result.Explanation = "تم اعتماد الضمانات غير المنتهية زمنيًا وذات الحالة التشغيلية النشطة، بشرط أن يكون نوع المرجع أمر شراء.";

            result.Facts.Add(new OperationalInquiryFact { Label = "العدد", Value = matchingGuarantees.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي المبالغ", Value = ArabicAmountFormatter.FormatSaudiRiyals(totalAmount) });
            result.Facts.Add(new OperationalInquiryFact { Label = "قريب الانتهاء", Value = matchingGuarantees.Count(g => g.IsExpiringSoon).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أول انتهاء قادم", Value = matchingGuarantees.FirstOrDefault() is { } firstExpiry ? DualCalendarDateService.FormatDate(firstExpiry.ExpiryDate, firstExpiry.DateCalendar) : "---" });

            foreach (Guarantee guarantee in matchingGuarantees.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = guarantee.ExpiryDate,
                    DateCalendar = guarantee.DateCalendar,
                    Title = guarantee.GuaranteeNo,
                    Details = $"{guarantee.Supplier} | {guarantee.ReferenceTypeLabel}: {guarantee.ReferenceNumber} | {ArabicAmountFormatter.FormatSaudiRiyals(guarantee.Amount)}"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetContractRelatedReleasedLastWeek()
        {
            DateTime today = DateTime.Now.Date;
            DateTime start = today.AddDays(-7);
            DateTime end = today.AddDays(-1);

            List<WorkflowRequestListItem> matchingRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Release,
                    RequestStatus = RequestStatus.Executed,
                    ReferenceType = GuaranteeReferenceType.Contract,
                    RequireReferenceNumber = true,
                    ResponseRecordedFrom = start,
                    ResponseRecordedTo = end,
                    SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
                });

            int uniqueGuarantees = matchingRequests
                .Select(item => item.RootGuaranteeId)
                .Distinct()
                .Count();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"contract-releases:{start:yyyyMMdd}:{end:yyyyMMdd}",
                Title = "كم عدد الضمانات الخاصة بالعقود التي أفرجنا عنها خلال الأسبوع الفائت؟",
                Subject = $"الفترة: {DualCalendarDateService.FormatGregorianDate(start)} إلى {DualCalendarDateService.FormatGregorianDate(end)}",
                EventDate = end
            };

            result.Answer = matchingRequests.Any()
                ? $"تم الإفراج عن {uniqueGuarantees} ضمانًا/ضمانات مرتبطة بالعقود خلال الفترة من {DualCalendarDateService.FormatGregorianDate(start)} إلى {DualCalendarDateService.FormatGregorianDate(end)}، عبر {matchingRequests.Count} طلب/طلبات إفراج منفذة."
                : $"لا توجد طلبات إفراج منفذة مرتبطة بالعقود خلال الفترة من {DualCalendarDateService.FormatGregorianDate(start)} إلى {DualCalendarDateService.FormatGregorianDate(end)}.";
            result.Explanation = "الاحتساب اعتمد طلبات الإفراج المنفذة فقط، مع اعتبار الضمان متعلقًا بالعقود متى كان نوع المرجع فيه عقدًا.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات المختلفة", Value = uniqueGuarantees.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد طلبات الإفراج", Value = matchingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = DualCalendarDateService.FormatGregorianDate(start) });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = DualCalendarDateService.FormatGregorianDate(end) });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"إفراج منفذ - {item.GuaranteeNo}",
                    Details = $"{item.Supplier} | {item.ReferenceTypeLabel}: {item.ReferenceNumber}"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetEmployeeCreatedContractRequestsLastMonth(string employeeName)
        {
            string normalizedEmployeeName = employeeName?.Trim()
                ?? throw new InvalidOperationException("اسم الموظف مطلوب.");

            if (string.IsNullOrWhiteSpace(normalizedEmployeeName))
            {
                throw new InvalidOperationException("اسم الموظف مطلوب.");
            }

            DateTime start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
            DateTime end = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1);

            List<WorkflowRequestListItem> matchingRequests = _databaseService
                .QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Extension,
                    CreatedBy = normalizedEmployeeName,
                    ReferenceType = GuaranteeReferenceType.Contract,
                    RequireReferenceNumber = true,
                    RequestDateFrom = start,
                    RequestDateTo = end,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
                })
                .Concat(_databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Release,
                    CreatedBy = normalizedEmployeeName,
                    ReferenceType = GuaranteeReferenceType.Contract,
                    RequireReferenceNumber = true,
                    RequestDateFrom = start,
                    RequestDateTo = end,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
                }))
                .OrderByDescending(item => item.Request.RequestDate)
                .ThenBy(item => item.GuaranteeNo)
                .ToList();

            int extensionCount = matchingRequests.Count(item => item.Request.Type == RequestType.Extension);
            int releaseCount = matchingRequests.Count(item => item.Request.Type == RequestType.Release);
            int uniqueGuarantees = matchingRequests.Select(item => item.RootGuaranteeId).Distinct().Count();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"employee-contract-requests:{normalizedEmployeeName.ToUpperInvariant()}:{start:yyyyMM}",
                Title = "كم طلب تمديد أو إفراج أنشأه موظف محدد الشهر الماضي للعقود؟",
                Subject = $"الموظف: {normalizedEmployeeName} | الفترة: {DualCalendarDateService.FormatGregorianDate(start)} إلى {DualCalendarDateService.FormatGregorianDate(end)}",
                EventDate = end
            };

            result.Answer = matchingRequests.Any()
                ? $"أنشأ الموظف {normalizedEmployeeName} عدد {matchingRequests.Count} طلب/طلبات تمديد أو إفراج خلال الشهر الماضي للضمانات المتعلقة بالعقود، منها {extensionCount} تمديد و{releaseCount} إفراج، وتخص {uniqueGuarantees} ضمانًا مختلفًا."
                : $"لم يُسجل على الموظف {normalizedEmployeeName} أي طلب تمديد أو إفراج خلال الشهر الماضي للضمانات المتعلقة بالعقود.";
            result.Explanation = "الاحتساب اعتمد تاريخ إنشاء الطلب واسم منشئ الطلب المحفوظ، مع قصر النتائج على الضمانات التي يكون نوع مرجعها عقدًا.";

            result.Facts.Add(new OperationalInquiryFact { Label = "اسم الموظف", Value = normalizedEmployeeName });
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي الطلبات", Value = matchingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "طلبات التمديد", Value = extensionCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "طلبات الإفراج", Value = releaseCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات المختلفة", Value = uniqueGuarantees.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = DualCalendarDateService.FormatGregorianDate(start) });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = DualCalendarDateService.FormatGregorianDate(end) });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.RequestDate,
                    DateCalendar = item.CurrentDateCalendar,
                    Title = $"{item.Request.TypeLabel} - {item.GuaranteeNo}",
                    Details = $"{item.Supplier} | {item.ReferenceTypeLabel}: {item.ReferenceNumber} | {item.Request.StatusLabel}"
                });
            }

            return result;
        }

        public OperationalInquiryResult GetExpiredPurchaseOrderOnlyWithoutExtensionAmount()
        {
            List<WorkflowRequestListItem> requests = _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });
            HashSet<int> rootsWithPendingRelease = requests
                .Where(item => item.Request.Type == RequestType.Release && item.Request.Status == RequestStatus.Pending)
                .Select(item => item.RootGuaranteeId)
                .ToHashSet();

            HashSet<int> rootsWithClosedRelease = requests
                .Where(item => item.Request.Type == RequestType.Release && item.Request.Status != RequestStatus.Pending)
                .Select(item => item.RootGuaranteeId)
                .ToHashSet();

            List<Guarantee> matchingGuarantees = _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    ReferenceType = GuaranteeReferenceType.PurchaseOrder,
                    RequireReferenceNumber = true,
                    TimeStatus = GuaranteeTimeStatus.Expired,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                })
                .Where(g => g.NeedsExpiryFollowUp)
                .ToList();

            decimal totalAmount = matchingGuarantees.Sum(g => g.Amount);
            int withPendingReleaseCount = matchingGuarantees.Count(g => rootsWithPendingRelease.Contains(g.RootId ?? g.Id));
            int withoutPendingReleaseCount = matchingGuarantees.Count - withPendingReleaseCount;
            int withClosedReleaseAttemptCount = matchingGuarantees.Count(g => rootsWithClosedRelease.Contains(g.RootId ?? g.Id));

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = "expired-po-needing-release",
                Title = "كم مبلغ ضمانات أوامر الشراء المنتهية التي تحتاج إفراجًا؟",
                Subject = "الضمانات المنتهية - أوامر الشراء فقط - تحتاج إفراج/إعادة",
                EventDate = DateTime.Now
            };

            result.Answer = matchingGuarantees.Any()
                ? $"إجمالي مبالغ ضمانات أوامر الشراء فقط المنتهية التي تحتاج إفراجًا أو إعادة للبنك هو {ArabicAmountFormatter.FormatSaudiRiyals(totalAmount)}، موزعة على {matchingGuarantees.Count} ضمانًا/ضمانات."
                : "لا توجد حاليًا ضمانات أوامر شراء فقط منتهية وتحتاج إفراجًا.";
            result.Explanation = "تم احتساب الضمانات المنتهية زمنيًا والتي لم تغلق دورة حياتها بعد. بعد الانتهاء لا يُطلب تمديد أو تسييل؛ الإجراء المتاح هو الإفراج/إعادة الضمان للبنك.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات", Value = matchingGuarantees.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي المبالغ", Value = ArabicAmountFormatter.FormatSaudiRiyals(totalAmount) });
            result.Facts.Add(new OperationalInquiryFact { Label = "بطلب إفراج معلق", Value = withPendingReleaseCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بدون طلب إفراج معلق", Value = withoutPendingReleaseCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "لها محاولة إفراج مغلقة", Value = withClosedReleaseAttemptCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم تاريخ انتهاء", Value = matchingGuarantees.FirstOrDefault() is { } firstExpired ? DualCalendarDateService.FormatDate(firstExpired.ExpiryDate, firstExpired.DateCalendar) : "---" });

            foreach (Guarantee guarantee in matchingGuarantees.Take(10))
            {
                int rootId = guarantee.RootId ?? guarantee.Id;
                string releaseState = rootsWithPendingRelease.Contains(rootId)
                    ? "له طلب إفراج معلق"
                    : rootsWithClosedRelease.Contains(rootId)
                        ? "له محاولة إفراج مغلقة"
                        : "لا يوجد طلب إفراج معلق";

                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = guarantee.ExpiryDate,
                    DateCalendar = guarantee.DateCalendar,
                    Title = guarantee.GuaranteeNo,
                    Details = $"{guarantee.Supplier} | {guarantee.ReferenceTypeLabel}: {guarantee.ReferenceNumber} | {ArabicAmountFormatter.FormatSaudiRiyals(guarantee.Amount)} | {releaseState}"
                });
            }

            return result;
        }
    }
}
