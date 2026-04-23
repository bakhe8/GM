using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;

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
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم تاريخ طلب", Value = oldest.Request.RequestDate.ToString("yyyy-MM-dd") });
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
                Subject = $"الفترة: {startOfMonth:yyyy-MM-dd} إلى {now:yyyy-MM-dd}",
                EventDate = now
            };

            result.Answer = matchingRequests.Any()
                ? $"تم تنفيذ {matchingRequests.Count} طلب/طلبات تمديد هذا الشهر، تمثل {uniqueGuarantees} ضمانًا مختلفًا."
                : "لا توجد طلبات تمديد منفذة مسجلة خلال هذا الشهر حتى الآن.";
            result.Explanation = "الاحتساب تم بناءً على طلبات التمديد التي حالتها منفذ وتاريخ تسجيل استجابة البنك عليها يقع ضمن الشهر الحالي.";

            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي طلبات التمديد المنفذة", Value = matchingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات المختلفة", Value = uniqueGuarantees.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = startOfMonth.ToString("yyyy-MM-dd") });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = now.ToString("yyyy-MM-dd") });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
                    Title = $"تمديد منفذ - {item.GuaranteeNo}",
                    Details = $"{item.Supplier} | الانتهاء الحالي {item.CurrentExpiryDate:yyyy-MM-dd}"
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
                ? $"يوجد {matchingGuarantees.Count} ضمانًا/ضمانات سارية تخص أوامر الشراء فقط، بإجمالي مبالغ {totalAmount:N2}."
                : "لا توجد حاليًا ضمانات سارية تخص أوامر الشراء فقط.";
            result.Explanation = "تم اعتماد الضمانات غير المنتهية زمنيًا وذات الحالة التشغيلية النشطة، بشرط أن يكون نوع المرجع أمر شراء.";

            result.Facts.Add(new OperationalInquiryFact { Label = "العدد", Value = matchingGuarantees.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي المبالغ", Value = totalAmount.ToString("N2") });
            result.Facts.Add(new OperationalInquiryFact { Label = "قريب الانتهاء", Value = matchingGuarantees.Count(g => g.IsExpiringSoon).ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أول انتهاء قادم", Value = matchingGuarantees.FirstOrDefault()?.ExpiryDate.ToString("yyyy-MM-dd") ?? "---" });

            foreach (Guarantee guarantee in matchingGuarantees.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = guarantee.ExpiryDate,
                    Title = guarantee.GuaranteeNo,
                    Details = $"{guarantee.Supplier} | {guarantee.ReferenceTypeLabel}: {guarantee.ReferenceNumber} | {guarantee.Amount:N2}"
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
                Subject = $"الفترة: {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd}",
                EventDate = end
            };

            result.Answer = matchingRequests.Any()
                ? $"تم الإفراج عن {uniqueGuarantees} ضمانًا/ضمانات مرتبطة بالعقود خلال الفترة من {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd}، عبر {matchingRequests.Count} طلب/طلبات إفراج منفذة."
                : $"لا توجد طلبات إفراج منفذة مرتبطة بالعقود خلال الفترة من {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd}.";
            result.Explanation = "الاحتساب اعتمد طلبات الإفراج المنفذة فقط، مع اعتبار الضمان متعلقًا بالعقود متى كان نوع المرجع فيه عقدًا.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات المختلفة", Value = uniqueGuarantees.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد طلبات الإفراج", Value = matchingRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = start.ToString("yyyy-MM-dd") });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = end.ToString("yyyy-MM-dd") });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.ResponseRecordedAt ?? item.Request.RequestDate,
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
                Subject = $"الموظف: {normalizedEmployeeName} | الفترة: {start:yyyy-MM-dd} إلى {end:yyyy-MM-dd}",
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
            result.Facts.Add(new OperationalInquiryFact { Label = "بداية الفترة", Value = start.ToString("yyyy-MM-dd") });
            result.Facts.Add(new OperationalInquiryFact { Label = "نهاية الفترة", Value = end.ToString("yyyy-MM-dd") });

            foreach (WorkflowRequestListItem item in matchingRequests.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = item.Request.RequestDate,
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
                RequestType = RequestType.Extension,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });
            HashSet<int> rootsWithExecutedExtension = requests
                .Where(item => item.Request.Type == RequestType.Extension && item.Request.Status == RequestStatus.Executed)
                .Select(item => item.RootGuaranteeId)
                .ToHashSet();

            HashSet<int> rootsWithAnyExtensionRequest = requests
                .Where(item => item.Request.Type == RequestType.Extension)
                .Select(item => item.RootGuaranteeId)
                .ToHashSet();

            List<Guarantee> matchingGuarantees = _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    LifecycleStatus = GuaranteeLifecycleStatus.Active,
                    ReferenceType = GuaranteeReferenceType.PurchaseOrder,
                    RequireReferenceNumber = true,
                    TimeStatus = GuaranteeTimeStatus.Expired,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                })
                .Where(g => !rootsWithExecutedExtension.Contains(g.RootId ?? g.Id))
                .ToList();

            decimal totalAmount = matchingGuarantees.Sum(g => g.Amount);
            int withoutAnyExtensionRequestCount = matchingGuarantees.Count(g => !rootsWithAnyExtensionRequest.Contains(g.RootId ?? g.Id));
            int withUnexecutedExtensionAttemptCount = matchingGuarantees.Count - withoutAnyExtensionRequestCount;

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = "expired-po-without-executed-extension",
                Title = "كم مبلغ ضمانات أوامر الشراء المنتهية بلا تمديد؟",
                Subject = "الضمانات المنتهية - أوامر الشراء فقط - بدون تمديد منفذ",
                EventDate = DateTime.Now
            };

            result.Answer = matchingGuarantees.Any()
                ? $"إجمالي مبالغ ضمانات أوامر الشراء فقط المنتهية بدون أي تمديد منفذ هو {totalAmount:N2}، موزعة على {matchingGuarantees.Count} ضمانًا/ضمانات."
                : "لا توجد حاليًا ضمانات أوامر شراء فقط منتهية بدون تمديد منفذ.";
            result.Explanation = "تم احتساب الضمانات المنتهية زمنيًا، ذات الحالة التشغيلية النشطة، والتي نوع مرجعها أمر شراء، مع استبعاد أي ضمان له طلب تمديد منفذ ضمن السلسلة.";

            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الضمانات", Value = matchingGuarantees.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي المبالغ", Value = totalAmount.ToString("N2") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بدون أي طلب تمديد", Value = withoutAnyExtensionRequestCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "بطلبات تمديد غير منفذة", Value = withUnexecutedExtensionAttemptCount.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "أقدم تاريخ انتهاء", Value = matchingGuarantees.FirstOrDefault()?.ExpiryDate.ToString("yyyy-MM-dd") ?? "---" });

            foreach (Guarantee guarantee in matchingGuarantees.Take(10))
            {
                int rootId = guarantee.RootId ?? guarantee.Id;
                string extensionState = rootsWithAnyExtensionRequest.Contains(rootId)
                    ? "له محاولة تمديد غير منفذة"
                    : "لا يوجد طلب تمديد";

                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = guarantee.ExpiryDate,
                    Title = guarantee.GuaranteeNo,
                    Details = $"{guarantee.Supplier} | {guarantee.ReferenceTypeLabel}: {guarantee.ReferenceNumber} | {guarantee.Amount:N2} | {extensionState}"
                });
            }

            return result;
        }
    }
}
