using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowAnnulmentExecutor
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;

        public WorkflowAnnulmentExecutor(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
        }

        public int Execute(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            if (string.IsNullOrWhiteSpace(responseAttachmentSourcePath))
                throw new ApplicationOperationException(
                    "WorkflowAnnulmentExecutor.Execute",
                    "تنفيذ النقض يستلزم مستند رسمي معتمد من البنك. يرجى إرفاق المستند قبل المتابعة.");

            List<StagedAttachmentFile> stagedResponseAttachments = _attachmentStorage.StageCopies(
                string.IsNullOrWhiteSpace(responseAttachmentSourcePath)
                    ? Array.Empty<string>()
                    : new[] { responseAttachmentSourcePath });
            int newGuaranteeId = 0;

            using var connection = SqliteConnectionFactory.Open(_connectionString);

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    DateTime executedAt = DateTime.Now;
                    WorkflowExecutionContext context = WorkflowExecutionDataAccess.LoadContext(
                        requestId,
                        RequestType.Annulment,
                        "نوع الطلب لا يطابق طريقة تنفيذ النقض.",
                        "لا يمكن تنفيذ طلب نقض غير معلق.",
                        connection,
                        transaction);

                    int nextVersionNumber = WorkflowExecutionDataAccess.GetNextVersionNumber(
                        context.Request.RootGuaranteeId,
                        connection,
                        transaction);
                    WorkflowExecutionDataAccess.ClearCurrentGuaranteeFlag(
                        context.Request.RootGuaranteeId,
                        connection,
                        transaction);

                    string annulmentNote = $"نقض {context.CurrentGuarantee.LifecycleStatusLabel} رقم {context.Request.SequenceNumber} بتاريخ {executedAt:yyyy-MM-dd}.";
                    newGuaranteeId = WorkflowExecutionDataAccess.InsertGuaranteeVersion(
                        context.CurrentGuarantee,
                        context.Request.RootGuaranteeId,
                        nextVersionNumber,
                        context.CurrentGuarantee.Amount,
                        context.CurrentGuarantee.ExpiryDate,
                        WorkflowExecutionDataAccess.AppendNote(context.CurrentGuarantee.Notes, annulmentNote),
                        GuaranteeLifecycleStatus.Active,
                        context.CurrentGuarantee.ReplacesRootId,
                        context.CurrentGuarantee.ReplacedByRootId,
                        executedAt,
                        connection,
                        transaction);

                    WorkflowExecutionDataAccess.CopyAttachments(
                        context.CurrentGuarantee.Attachments,
                        newGuaranteeId,
                        executedAt,
                        connection,
                        transaction);

                    if (stagedResponseAttachments.Count > 0)
                    {
                        StagedAttachmentFile stagedResponseAttachment = stagedResponseAttachments[0];
                        WorkflowExecutionDataAccess.InsertAttachmentRecord(
                            newGuaranteeId,
                            stagedResponseAttachment.OriginalFileName,
                            stagedResponseAttachment.SavedFileName,
                            stagedResponseAttachment.FileExtension,
                            executedAt,
                            connection,
                            transaction);
                    }

                    WorkflowExecutionDataAccess.UpdateWorkflowRequestAsExecuted(
                        requestId,
                        responseNotes ?? string.Empty,
                        responseOriginalFileName,
                        responseSavedFileName,
                        newGuaranteeId,
                        executedAt,
                        connection,
                        transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    _attachmentStorage.CleanupStagedCopies(stagedResponseAttachments);
                    throw;
                }
            }

            _attachmentStorage.FinalizeStagedCopies(stagedResponseAttachments, "ExecuteAnnulmentWorkflowRequest");
            SimpleLogger.LogAudit(
                "Workflow.Annulment",
                $"RequestId={requestId}",
                $"NewVersionId={newGuaranteeId}");
            return newGuaranteeId;
        }
    }
}
