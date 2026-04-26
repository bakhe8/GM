using System;

namespace GuaranteeManager
{
    public sealed class ShellWorkspaceSearchPlan
    {
        public ShellWorkspaceSearchPlan(string targetWorkspaceKey, string searchText, bool matchedAlias)
        {
            TargetWorkspaceKey = string.IsNullOrWhiteSpace(targetWorkspaceKey)
                ? ShellWorkspaceKeys.Guarantees
                : targetWorkspaceKey;
            SearchText = searchText?.Trim() ?? string.Empty;
            MatchedAlias = matchedAlias;
        }

        public string TargetWorkspaceKey { get; }

        public string SearchText { get; }

        public bool MatchedAlias { get; }

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        public static ShellWorkspaceSearchPlan Empty { get; } = new(ShellWorkspaceKeys.Guarantees, string.Empty, false);
    }
}
