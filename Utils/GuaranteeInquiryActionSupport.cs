using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Utils
{
    public static class GuaranteeInquiryActionSupport
    {
        private static readonly string[] InquiryPrefixes =
        {
            "guarantee.",
            "bank.",
            "supplier.",
            "summary.",
            "employee."
        };

        public static bool IsInquiryAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            return InquiryPrefixes.Any(prefix => actionId.StartsWith(prefix, StringComparison.Ordinal));
        }

        public static IReadOnlyList<ContextActionSection> BuildSections(IContextActionService contextActionService)
        {
            ContextActionSection? inquiryRoot = contextActionService.GetGuaranteeActions()
                .FirstOrDefault(section => section.Header == "افهم");

            if (inquiryRoot == null)
            {
                return Array.Empty<ContextActionSection>();
            }

            var sections = new List<ContextActionSection>();
            foreach (ContextActionDefinition item in inquiryRoot.Items)
            {
                if (item.HasChildren)
                {
                    sections.Add(new ContextActionSection(item.Header, item.Description, item.Children.ToArray()));
                    continue;
                }

                sections.Add(new ContextActionSection(inquiryRoot.Header, inquiryRoot.Description, item));
            }

            return sections;
        }

        public static ContextActionAvailability GetAvailability(string actionId, Guarantee guarantee)
        {
            return actionId switch
            {
                "bank.pending-requests" or "bank.confirmation" when string.IsNullOrWhiteSpace(guarantee.Bank)
                    => ContextActionAvailability.Disabled("لا يوجد بنك مرتبط بهذا السجل."),
                "supplier.latest-activity" when string.IsNullOrWhiteSpace(guarantee.Supplier)
                    => ContextActionAvailability.Disabled("لا يوجد مورد مرتبط بهذا السجل."),
                _ => ContextActionAvailability.Enabled()
            };
        }

        public static OperationalInquiryResult? Execute(
            string actionId,
            Guarantee guarantee,
            IOperationalInquiryService inquiryService,
            Func<string?> promptForEmployeeName)
        {
            return actionId switch
            {
                "guarantee.last-event" => inquiryService.GetLastEventForGuarantee(guarantee.Id),
                "guarantee.extension-timing" => inquiryService.GetExtensionTimingForGuarantee(guarantee.Id),
                "guarantee.outstanding-extension" => inquiryService.GetOutstandingReasonForGuarantee(guarantee.Id, RequestType.Extension),
                "guarantee.outstanding-release" => inquiryService.GetOutstandingReasonForGuarantee(guarantee.Id, RequestType.Release),
                "guarantee.outstanding-liquidation" => inquiryService.GetOutstandingReasonForGuarantee(guarantee.Id, RequestType.Liquidation),
                "guarantee.expired-without-extension" => inquiryService.GetExpiredWithoutExtensionReasonForGuarantee(guarantee.Id),
                "guarantee.release-evidence" => inquiryService.GetReleaseEvidenceForGuarantee(guarantee.Id),
                "guarantee.liquidation-evidence" => inquiryService.GetLiquidationEvidenceForGuarantee(guarantee.Id),
                "guarantee.reduction-source" => inquiryService.GetReductionSourceForGuarantee(guarantee.Id),
                "guarantee.response-link-status" => inquiryService.GetResponseDocumentLinkStatusForGuarantee(guarantee.Id),
                "bank.pending-requests" when !string.IsNullOrWhiteSpace(guarantee.Bank)
                    => inquiryService.GetPendingRequestsForBank(guarantee.Bank),
                "bank.confirmation" when !string.IsNullOrWhiteSpace(guarantee.Bank)
                    => inquiryService.GetBankConfirmationSummary(guarantee.Bank),
                "supplier.latest-activity" when !string.IsNullOrWhiteSpace(guarantee.Supplier)
                    => inquiryService.GetLatestActivityForSupplier(guarantee.Supplier),
                "summary.executed-extensions-this-month" => inquiryService.GetExecutedExtensionsThisMonth(),
                "summary.active-po-only" => inquiryService.GetActivePurchaseOrderOnlyGuarantees(),
                "summary.contract-released-last-week" => inquiryService.GetContractRelatedReleasedLastWeek(),
                "summary.oldest-pending" => inquiryService.GetTopOldestPendingRequests(),
                "summary.expired-po-without-extension" => inquiryService.GetExpiredPurchaseOrderOnlyWithoutExtensionAmount(),
                "employee.contract-requests-last-month" => ExecuteEmployeeInquiry(inquiryService, promptForEmployeeName),
                _ => null
            };
        }

        private static OperationalInquiryResult? ExecuteEmployeeInquiry(
            IOperationalInquiryService inquiryService,
            Func<string?> promptForEmployeeName)
        {
            string? employeeName = promptForEmployeeName.Invoke()?.Trim();
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                return null;
            }

            return inquiryService.GetEmployeeCreatedContractRequestsLastMonth(employeeName);
        }
    }
}
