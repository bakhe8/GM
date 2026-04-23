using System;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowVerificationExecutor
    {
        private readonly string _connectionString;
        private readonly WorkflowNewVersionExecutor _newVersionExecutor;

        public WorkflowVerificationExecutor(string connectionString, WorkflowNewVersionExecutor newVersionExecutor)
        {
            _connectionString = connectionString;
            _newVersionExecutor = newVersionExecutor;
        }

        public int? Execute(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            if (promoteResponseDocumentToOfficialAttachment)
            {
                if (string.IsNullOrWhiteSpace(responseAttachmentSourcePath))
                {
                    throw new InvalidOperationException("لا يمكن إنشاء نسخة جديدة لطلب التحقق بدون مستند رد بنك معتمد.");
                }

                return _newVersionExecutor.Execute(
                    requestId,
                    RequestType.Verification,
                    null,
                    null,
                    null,
                    responseNotes,
                    responseOriginalFileName,
                    responseSavedFileName,
                    responseAttachmentSourcePath,
                    false);
            }

            using var connection = SqliteConnectionFactory.Open(_connectionString);

            using var transaction = connection.BeginTransaction();
            try
            {
                DateTime executedAt = DateTime.Now;
                WorkflowExecutionDataAccess.LoadContext(
                    requestId,
                    RequestType.Verification,
                    "نوع الطلب لا يطابق طريقة تنفيذ التحقق.",
                    "لا يمكن تنفيذ طلب تحقق غير معلق.",
                    connection,
                    transaction);

                WorkflowExecutionDataAccess.UpdateWorkflowRequestAsExecuted(
                    requestId,
                    responseNotes ?? string.Empty,
                    responseOriginalFileName,
                    responseSavedFileName,
                    null,
                    executedAt,
                    connection,
                    transaction);

                transaction.Commit();
                return null;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
