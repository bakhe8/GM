using System;

namespace GuaranteeManager
{
    public sealed class ShellWorkspaceSearchPlan
    {
        public ShellWorkspaceSearchPlan(
            string targetWorkspaceKey,
            string searchText,
            bool matchedAlias,
            string? initialScopeFilter = null)
        {
            TargetWorkspaceKey = string.IsNullOrWhiteSpace(targetWorkspaceKey)
                ? ShellWorkspaceKeys.Guarantees
                : targetWorkspaceKey;
            SearchText = searchText?.Trim() ?? string.Empty;
            MatchedAlias = matchedAlias;
            InitialScopeFilter = initialScopeFilter?.Trim() ?? string.Empty;
        }

        public string TargetWorkspaceKey { get; }

        public string SearchText { get; }

        public bool MatchedAlias { get; }

        public string InitialScopeFilter { get; }

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        public bool HasInitialScopeFilter => !string.IsNullOrWhiteSpace(InitialScopeFilter);

        public static ShellWorkspaceSearchPlan Empty { get; } = new(ShellWorkspaceKeys.Guarantees, string.Empty, false);
    }
}
