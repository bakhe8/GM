using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Utils
{
    internal static class InquiryFileRoutingResolver
    {
        public static bool TryResolve(OperationalInquiryResult inquiryResult, out GuaranteeFileFocusArea focusArea, out int? requestIdToFocus)
        {
            requestIdToFocus = inquiryResult.RelatedRequest?.Id;

            if (StartsWith(inquiryResult, "last-event:"))
            {
                focusArea = GuaranteeFileFocusArea.Series;
                return true;
            }

            if (StartsWith(inquiryResult, "outstanding-") ||
                StartsWith(inquiryResult, "extension-timing:") ||
                StartsWith(inquiryResult, "expired-no-extension:") ||
                StartsWith(inquiryResult, "reduction-source:"))
            {
                focusArea = GuaranteeFileFocusArea.Requests;
                return true;
            }

            if (StartsWith(inquiryResult, "release-evidence:") ||
                StartsWith(inquiryResult, "liquidation-evidence:") ||
                StartsWith(inquiryResult, "response-link:"))
            {
                focusArea = GuaranteeFileFocusArea.Outputs;
                return true;
            }

            focusArea = GuaranteeFileFocusArea.Series;
            return false;
        }

        private static bool StartsWith(OperationalInquiryResult inquiryResult, string prefix)
        {
            return inquiryResult.InquiryKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
