using System;

namespace GuaranteeManager.Services
{
    public sealed record UiShellDiagnosticsState(
        DateTimeOffset Timestamp,
        string Reason,
        string CurrentWorkspaceKey,
        string ActiveWorkspaceContentType,
        string GlobalSearchText,
        string GuaranteesSearchText,
        string SelectedBank,
        string SelectedGuaranteeType,
        string SelectedTimeStatus,
        int VisibleGuaranteeCount,
        string FooterSummary,
        string PendingRequestCount,
        string ExpiredCount,
        string ExpiredFollowUpCount,
        string ExpiringSoonCount,
        string ActiveCount,
        bool HasLastFile,
        string LastFileGuaranteeNo,
        string LastFileSummary,
        int? SelectedGuaranteeId,
        int? SelectedGuaranteeRootId,
        string SelectedGuaranteeNo,
        string SelectedGuaranteeSupplier,
        string SelectedGuaranteeBank);
}
