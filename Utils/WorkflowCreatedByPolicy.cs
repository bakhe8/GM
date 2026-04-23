using System;

namespace GuaranteeManager.Utils
{
    internal static class WorkflowCreatedByPolicy
    {
        public const string LegacyValue = "Legacy";
        public const string SystemValue = "System";

        public static string NormalizeForNewRequest(string? createdBy)
        {
            string value = createdBy?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string environmentUser = Environment.UserName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(environmentUser) ? SystemValue : environmentUser;
        }
    }
}
