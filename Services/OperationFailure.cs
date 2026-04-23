using System;

namespace GuaranteeManager.Services
{
    internal static class OperationFailure
    {
        public static Exception LogAndWrap(
            Exception ex,
            string operationName,
            string userMessage,
            bool isCritical = false)
        {
            if (ex is ApplicationOperationException or InvalidOperationException or DeferredFilePromotionException)
            {
                return ex;
            }

            SimpleLogger.LogError(ex, operationName);
            return new ApplicationOperationException(
                operationName,
                userMessage,
                OperationLogScope.CurrentOperationId,
                ex,
                isCritical);
        }
    }
}
