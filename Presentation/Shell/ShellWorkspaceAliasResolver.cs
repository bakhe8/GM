using System;
using System.Collections.Generic;
using System.Linq;

namespace GuaranteeManager
{
    public static class ShellWorkspaceAliasResolver
    {
        private static readonly IReadOnlyDictionary<string, string[]> AliasMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [ShellWorkspaceKeys.Dashboard] = new[] { "اليوم", "لوحة التحكم", "الرئيسية", "dashboard", "home", "today" },
                [ShellWorkspaceKeys.Guarantees] = new[] { "الضمانات", "ضمان", "guarantees", "guarantee" },
                [ShellWorkspaceKeys.Requests] = new[] { "الطلبات", "طلبات", "requests", "request" },
                [ShellWorkspaceKeys.Banks] = new[] { "البنوك", "بنوك", "banks", "bank" },
                [ShellWorkspaceKeys.Reports] = new[] { "التقارير", "تقارير", "التحليلات", "تحليلات", "المخرجات", "analytics", "analysis", "outputs", "reports", "report" },
                [ShellWorkspaceKeys.Settings] = new[] { "الإعدادات", "المسارات", "settings", "paths" }
            };

        private static readonly string[] FollowUpAliases =
        {
            "التنبيهات",
            "تنبيهات",
            "المتابعات",
            "متابعات",
            "notifications",
            "notification",
            "alerts",
            "alert",
            "followups",
            "follow-ups"
        };

        public static ShellWorkspaceSearchPlan Resolve(string? rawInput, string? currentWorkspaceKey)
        {
            string input = rawInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return ShellWorkspaceSearchPlan.Empty;
            }

            if (IsFollowUpAlias(input))
            {
                return CreateFollowUpPlan(string.Empty);
            }

            if (TryResolveExactAlias(input, out string aliasWorkspace))
            {
                return new ShellWorkspaceSearchPlan(aliasWorkspace, string.Empty, matchedAlias: true);
            }

            int separatorIndex = input.IndexOfAny(new[] { ':', '：' });
            if (separatorIndex > 0)
            {
                string workspaceToken = input[..separatorIndex].Trim();
                string searchText = input[(separatorIndex + 1)..].Trim();
                if (IsFollowUpAlias(workspaceToken))
                {
                    return CreateFollowUpPlan(searchText);
                }

                if (TryResolveExactAlias(workspaceToken, out aliasWorkspace))
                {
                    return new ShellWorkspaceSearchPlan(aliasWorkspace, searchText, matchedAlias: true);
                }
            }

            string targetWorkspace = NormalizeWorkspace(currentWorkspaceKey);
            if (string.Equals(targetWorkspace, ShellWorkspaceKeys.Dashboard, StringComparison.Ordinal))
            {
                targetWorkspace = ShellWorkspaceKeys.Guarantees;
            }

            return new ShellWorkspaceSearchPlan(targetWorkspace, input, matchedAlias: false);
        }

        private static ShellWorkspaceSearchPlan CreateFollowUpPlan(string rawSearchText)
        {
            string searchText = rawSearchText.Trim();
            string scopeFilter = DashboardScopeFilters.ExpiryFollowUps;

            if (IsExpiredFollowUpToken(searchText))
            {
                searchText = string.Empty;
                scopeFilter = DashboardScopeFilters.LegacyExpiredFollowUp;
            }
            else if (IsExpiringSoonFollowUpToken(searchText))
            {
                searchText = string.Empty;
                scopeFilter = DashboardScopeFilters.LegacyExpiringSoon;
            }

            return new ShellWorkspaceSearchPlan(
                ShellWorkspaceKeys.Dashboard,
                searchText,
                matchedAlias: true,
                initialScopeFilter: scopeFilter);
        }

        private static bool IsFollowUpAlias(string token)
        {
            string normalized = token.Trim();
            return FollowUpAliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExpiredFollowUpToken(string token)
        {
            string normalized = token.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return string.Equals(normalized, "منتهي", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "منتهية", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "منتهيه", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "expired", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "overdue", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExpiringSoonFollowUpToken(string token)
        {
            string normalized = token.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return string.Equals(normalized, "قريب", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "قريبة", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "قريب الانتهاء", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "قريبة الانتهاء", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "expiring", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "soon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "expiring soon", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveExactAlias(string token, out string workspaceKey)
        {
            string normalized = token.Trim();
            foreach ((string key, string[] aliases) in AliasMap)
            {
                if (aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    workspaceKey = key;
                    return true;
                }
            }

            workspaceKey = string.Empty;
            return false;
        }

        private static string NormalizeWorkspace(string? workspaceKey)
        {
            if (!string.IsNullOrWhiteSpace(workspaceKey) && AliasMap.ContainsKey(workspaceKey))
            {
                return workspaceKey;
            }

            return ShellWorkspaceKeys.Guarantees;
        }
    }
}
