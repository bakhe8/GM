using System;

namespace GuaranteeManager.Services
{
    public sealed class ApplicationOperationException : Exception
    {
        public ApplicationOperationException(
            string operationName,
            string userMessage,
            string? correlationId = null,
            Exception? innerException = null,
            bool isCritical = false)
            : base(BuildMessage(operationName, userMessage, correlationId), innerException)
        {
            OperationName = string.IsNullOrWhiteSpace(operationName) ? "UnknownOperation" : operationName;
            UserMessage = string.IsNullOrWhiteSpace(userMessage)
                ? "تعذر إكمال العملية الحالية بسبب خطأ تقني."
                : userMessage.Trim();
            CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                ? OperationLogScope.CurrentOperationId ?? Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
                : correlationId.Trim();
            IsCritical = isCritical;
        }

        public string OperationName { get; }

        public string UserMessage { get; }

        public string CorrelationId { get; }

        public bool IsCritical { get; }

        public string UserMessageWithReference =>
            $"{UserMessage}{Environment.NewLine}رقم المتابعة: {CorrelationId}";

        private static string BuildMessage(string operationName, string userMessage, string? correlationId)
        {
            return $"{userMessage} [Operation={operationName}, Ref={correlationId ?? OperationLogScope.CurrentOperationId ?? "-"}]";
        }
    }
}
