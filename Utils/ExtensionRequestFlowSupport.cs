using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Utils
{
    public static class ExtensionRequestFlowSupport
    {
        public static DateTime GetSuggestedRequestedExpiryDate(Guarantee guarantee)
        {
            DateTime currentExpiry = guarantee.ExpiryDate.Date;
            DateTime suggested = currentExpiry.AddYears(1);
            return suggested <= currentExpiry
                ? currentExpiry.AddDays(1)
                : suggested;
        }

        public static bool TryValidate(Guarantee guarantee, DateTime? requestedExpiryDate, string createdBy, out string reason)
        {
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.IsExpired)
            {
                reason = "طلب التمديد متاح فقط لضمان نشط ولم تنته صلاحيته. بعد الانتهاء يكون الإجراء المتاح هو الإفراج/إعادة الضمان.";
                return false;
            }

            if (!requestedExpiryDate.HasValue)
            {
                reason = "اختر التاريخ المطلوب بعد التمديد أولًا.";
                return false;
            }

            if (requestedExpiryDate.Value.Date <= guarantee.ExpiryDate.Date)
            {
                reason = "اجعل تاريخ التمديد المطلوب بعد تاريخ الانتهاء الحالي.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                reason = "أدخل اسم منشئ الطلب أولًا.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static string BuildReasonSummary(Guarantee guarantee)
        {
            return guarantee.IsExpiringSoon
                ? "هذا الضمان يقترب من الانتهاء ولا يوجد له طلب تمديد مفتوح الآن."
                : "أنت بصدد إنشاء طلب تمديد جديد على هذا الضمان من نفس السياق الحالي.";
        }

        public static string BuildEffectPreview(Guarantee guarantee, DateTime? requestedExpiryDate)
        {
            string requestedDateLabel = requestedExpiryDate.HasValue
                ? DualCalendarDateService.FormatDualDate(requestedExpiryDate.Value)
                : "---";

            return
                "بعد الحفظ:\n" +
                "• سيُنشأ طلب تمديد جديد بحالة قيد الانتظار\n" +
                $"• سيبقى تاريخ انتهاء الضمان الحالي كما هو حتى يرد البنك ({DualCalendarDateService.FormatDualDate(guarantee.ExpiryDate)})\n" +
                $"• سيُسجل التاريخ المطلوب في الطلب كقيمة مستهدفة ({requestedDateLabel})\n" +
                "• سيظهر الطلب مباشرة في قائمة الطلبات المرتبطة والسجل الزمني";
        }
    }
}
