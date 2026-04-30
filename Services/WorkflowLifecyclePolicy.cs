using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal static class WorkflowLifecyclePolicy
    {
        public static bool CanCreateRequest(Guarantee guarantee, RequestType requestType)
        {
            return requestType switch
            {
                RequestType.Release => IsReleaseEligible(guarantee),
                RequestType.Extension
                    or RequestType.Reduction
                    or RequestType.Liquidation
                    or RequestType.Verification
                    or RequestType.Replacement => IsActiveAndNotExpired(guarantee),
                _ => false
            };
        }

        public static bool CanExecuteRequest(Guarantee guarantee, RequestType requestType)
        {
            return CanCreateRequest(guarantee, requestType);
        }

        public static string GetCreateBlockedMessage(RequestType requestType, Guarantee guarantee)
        {
            if (requestType == RequestType.Release)
            {
                return "لا يمكن إنشاء طلب إفراج إلا لضمان نشط أو منتهي الصلاحية ولم تُغلق دورة حياته بعد.";
            }

            if (guarantee.IsExpired || guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Expired)
            {
                return $"لا يمكن إنشاء {GetRequestLabel(requestType)} لضمان منتهي الصلاحية. الإجراء المتاح بعد الانتهاء هو الإفراج/إعادة الضمان فقط.";
            }

            return $"لا يمكن إنشاء {GetRequestLabel(requestType)} لأن حالة الضمان الحالية هي {guarantee.LifecycleStatusLabel}.";
        }

        public static string GetExecutionBlockedMessage(RequestType requestType, Guarantee guarantee)
        {
            if (requestType == RequestType.Release)
            {
                return $"لا يمكن تنفيذ طلب الإفراج لأن حالة الضمان الحالية هي {guarantee.LifecycleStatusLabel} وليست نشطة أو منتهية الصلاحية.";
            }

            if (guarantee.IsExpired || guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Expired)
            {
                return $"لا يمكن تنفيذ {GetRequestLabel(requestType)} لأن الضمان منتهي الصلاحية. الإجراء المتاح بعد الانتهاء هو الإفراج/إعادة الضمان فقط.";
            }

            return $"لا يمكن تنفيذ {GetRequestLabel(requestType)} لأن حالة الضمان الحالية هي {guarantee.LifecycleStatusLabel}.";
        }

        private static bool IsReleaseEligible(Guarantee guarantee)
            => guarantee.LifecycleStatus is GuaranteeLifecycleStatus.Active or GuaranteeLifecycleStatus.Expired;

        private static bool IsActiveAndNotExpired(Guarantee guarantee)
            => guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active && !guarantee.IsExpired;

        private static string GetRequestLabel(RequestType requestType) => requestType switch
        {
            RequestType.Extension => "طلب تمديد",
            RequestType.Reduction => "طلب تخفيض",
            RequestType.Release => "طلب إفراج",
            RequestType.Liquidation => "طلب تسييل",
            RequestType.Verification => "طلب تحقق",
            RequestType.Replacement => "طلب استبدال",
            _ => "هذا الطلب"
        };
    }
}
