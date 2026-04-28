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
                [ShellWorkspaceKeys.Notifications] = new[] { "التنبيهات", "تنبيهات", "المتابعات", "notifications", "alerts" },
                [ShellWorkspaceKeys.Settings] = new[] { "الإعدادات", "المسارات", "settings", "paths" }
            };

        public static ShellWorkspaceSearchPlan Resolve(string? rawInput, string? currentWorkspaceKey)
        {
            string input = rawInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return ShellWorkspaceSearchPlan.Empty;
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
