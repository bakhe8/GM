using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal static class GuaranteeHistoryHealthAnalyzer
    {
        public static List<GuaranteeHistoryHealthFinding> BuildFindings(
            Guarantee current,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests)
        {
            var findings = new List<GuaranteeHistoryHealthFinding>();
            int totalAttachments = orderedHistory.Sum(item => item.AttachmentCount);
            List<WorkflowRequest> pendingRequests = orderedRequests
                .Where(item => item.Status == RequestStatus.Pending)
                .OrderBy(item => item.RequestDate)
                .ThenBy(item => item.SequenceNumber)
                .ToList();
            List<WorkflowRequest> executedWithoutResponseDocument = orderedRequests
                .Where(item => item.Status == RequestStatus.Executed && !item.HasResponseDocument)
                .OrderByDescending(item => item.ResponseRecordedAt ?? item.RequestDate)
                .ThenBy(item => item.SequenceNumber)
                .ToList();
            List<WorkflowRequest> requestsWithoutLetters = orderedRequests
                .Where(item => !item.HasLetter)
                .OrderBy(item => item.RequestDate)
                .ThenBy(item => item.SequenceNumber)
                .ToList();
            bool finalLifecycle = current.LifecycleStatus is GuaranteeLifecycleStatus.Released
                or GuaranteeLifecycleStatus.Liquidated
                or GuaranteeLifecycleStatus.Replaced;

            if (current.NeedsExpiryFollowUp)
            {
                findings.Add(new GuaranteeHistoryHealthFinding(
                    "إجراء مطلوب",
                    "انتهاء الضمان",
                    "الضمان منتهي زمنيًا وما زال مفتوحًا تشغيليًا.",
                    $"تاريخ الانتهاء: {current.ExpiryDate:yyyy/MM/dd} | الحالة التشغيلية: {current.LifecycleStatusLabel}",
                    "إنشاء أو متابعة طلب إفراج/إعادة للبنك وتوثيق رد البنك."));
            }

            if (pendingRequests.Any())
            {
                WorkflowRequest oldest = pendingRequests[0];
                int ageDays = Math.Max(0, (DateTime.Now.Date - oldest.RequestDate.Date).Days);
                findings.Add(new GuaranteeHistoryHealthFinding(
                    finalLifecycle ? "نقص دليل" : "متابعة",
                    "طلبات معلقة",
                    $"يوجد {pendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب/طلبات لم يسجل لها رد بنك.",
                    $"أقدم طلب: {oldest.TypeLabel} رقم {oldest.SequenceNumber.ToString("N0", CultureInfo.InvariantCulture)} مفتوح منذ {ageDays.ToString("N0", CultureInfo.InvariantCulture)} يوم/أيام.",
                    finalLifecycle
                        ? "مراجعة الطلبات العالقة لأنها لا يفترض أن تبقى مفتوحة بعد إنهاء دورة حياة الضمان."
                        : "متابعة البنك وتسجيل الرد أو إلحاق مستند الرد عند وصوله."));
            }

            if (executedWithoutResponseDocument.Any())
            {
                findings.Add(new GuaranteeHistoryHealthFinding(
                    "نقص دليل",
                    "مستندات رد البنك",
                    $"يوجد {executedWithoutResponseDocument.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب/طلبات منفذة بلا مستند رد بنك.",
                    BuildRequestSequenceSummary(executedWithoutResponseDocument),
                    "إلحاق مستند رد البنك من سجل الضمان حتى يكتمل الإثبات الرسمي."));
            }

            if (requestsWithoutLetters.Any())
            {
                findings.Add(new GuaranteeHistoryHealthFinding(
                    "نقص دليل",
                    "خطابات الطلب",
                    $"يوجد {requestsWithoutLetters.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب/طلبات بلا خطاب طلب محفوظ.",
                    BuildRequestSequenceSummary(requestsWithoutLetters),
                    "فتح الطلب ومراجعة خطاب الطلب أو إعادة حفظه عند الحاجة."));
            }

            if (totalAttachments == 0)
            {
                findings.Add(new GuaranteeHistoryHealthFinding(
                    "نقص دليل",
                    "مرفقات الضمان",
                    "لا توجد مرفقات محفوظة عبر إصدارات هذا الضمان.",
                    $"عدد الإصدارات: {orderedHistory.Count.ToString("N0", CultureInfo.InvariantCulture)}",
                    "إرفاق صورة الضمان أو المستند الرسمي المناسب من السجل الزمني."));
            }

            if (findings.Count == 0)
            {
                findings.Add(new GuaranteeHistoryHealthFinding(
                    "سليم",
                    "الفحص العام",
                    "لا توجد نواقص ظاهرة في الطلبات أو المرفقات حسب البيانات الحالية.",
                    $"الإصدارات: {orderedHistory.Count.ToString("N0", CultureInfo.InvariantCulture)} | الطلبات: {orderedRequests.Count.ToString("N0", CultureInfo.InvariantCulture)} | المرفقات: {totalAttachments.ToString("N0", CultureInfo.InvariantCulture)}",
                    "لا يلزم إجراء فوري."));
            }

            return findings;
        }

        private static string BuildRequestSequenceSummary(IReadOnlyList<WorkflowRequest> requests)
        {
            return string.Join(
                "، ",
                requests
                    .Take(5)
                    .Select(item => $"{item.TypeLabel} #{item.SequenceNumber.ToString("N0", CultureInfo.InvariantCulture)}"));
        }
    }

    internal sealed record GuaranteeHistoryHealthFinding(
        string Level,
        string Check,
        string Result,
        string Evidence,
        string Action);
}
