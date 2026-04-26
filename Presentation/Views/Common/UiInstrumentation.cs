using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;

namespace GuaranteeManager
{
    public static class UiInstrumentation
    {
        public static T Identify<T>(T element, string automationId, string? name = null) where T : DependencyObject
        {
            AutomationProperties.SetAutomationId(element, automationId);
            if (!string.IsNullOrWhiteSpace(name))
            {
                AutomationProperties.SetName(element, name);
            }

            return element;
        }

        public static string SanitizeAutomationKey(string prefix, string label)
        {
            string normalized = Regex.Replace(label ?? string.Empty, @"\s+", string.Empty);
            normalized = Regex.Replace(normalized, @"[^\p{L}\p{Nd}_\.]", string.Empty);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "Unnamed";
            }

            return $"{prefix}.{normalized}";
        }
    }
}
