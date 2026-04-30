using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowLifecycleStatusExecutor
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;

        public WorkflowLifecycleStatusExecutor(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
        }

        public int Execute(
            int requestId,
            RequestType expectedType,
            GuaranteeLifecycleStatus targetStatus,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath,
            bool cancelOtherPendingRequests,
            string? cancelOtherPendingRequestsNote = null)
        {
            List<StagedAttachmentFile> stagedResponseAttachments = _attachmentStorage.StageCopies(
                string.IsNullOrWhiteSpace(responseAttachmentSourcePath)
                    ? Array.Empty<string>()
                    : new[] { responseAttachmentSourcePath });
            int affectedGuaranteeId = 0;

            using var connection = SqliteConnectionFactory.Open(_connectionString);

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    DateTime executedAt = DateTime.Now;
                    WorkflowExecutionContext context = WorkflowExecutionDataAccess.LoadContext(
                        requestId,
                        expectedType,
                        "نوع الطلب لا يطابق طريقة التنفيذ المطلوبة.",
                        "لا يمكن تنفيذ طلب غير معلق.",
                        connection,
                        transaction);

                    string executionNote = $"تم تنفيذ {context.Request.TypeLabel} رقم {context.Request.SequenceNumber} بتاريخ {executedAt:yyyy-MM-dd} وإنهاء دورة حياة الضمان بحالة {GuaranteeLifecycleStatusDisplay.GetLabel(targetStatus)}.";
                    WorkflowExecutionDataAccess.UpdateGuaranteeLifecycleStatus(
                        context.CurrentGuarantee.Id,
                        targetStatus,
                        WorkflowExecutionDataAccess.AppendNote(context.CurrentGuarantee.Notes, executionNote),
                        context.CurrentGuarantee.ReplacedByRootId,
                        connection,
                        transaction);

                    if (stagedResponseAttachments.Count > 0)
                    {
                        StagedAttachmentFile stagedResponseAttachment = stagedResponseAttachments[0];
                        WorkflowExecutionDataAccess.InsertAttachmentRecord(
                            context.CurrentGuarantee.Id,
                            stagedResponseAttachment.OriginalFileName,
                            stagedResponseAttachment.SavedFileName,
                            stagedResponseAttachment.FileExtension,
                            executedAt,
                            connection,
                            transaction,
                            AttachmentDocumentType.BankResponse);
                    }

                    WorkflowExecutionDataAccess.UpdateWorkflowRequestAsExecuted(
                        requestId,
                        responseNotes ?? string.Empty,
                        responseOriginalFileName,
                        responseSavedFileName,
                        null,
                        executedAt,
                        connection,
                        transaction);

                    if (cancelOtherPendingRequests)
                    {
                        WorkflowExecutionDataAccess.SupersedePendingRequests(
                            context.Request.RootGuaranteeId,
                            requestId,
                            cancelOtherPendingRequestsNote ?? "أُسقط الطلب تلقائيًا بسبب تنفيذ طلب أنهى دورة حياة الضمان.",
                            executedAt,
                            connection,
                            transaction);
                    }

                    affectedGuaranteeId = context.CurrentGuarantee.Id;
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    _attachmentStorage.CleanupStagedCopies(stagedResponseAttachments);
                    throw;
                }
            }

            _attachmentStorage.FinalizeStagedCopies(stagedResponseAttachments, $"Execute{expectedType}WorkflowRequest");
            SimpleLogger.LogAudit(
                $"Workflow.{expectedType}",
                $"RequestId={requestId}",
                $"AffectedGuaranteeId={affectedGuaranteeId}");
            return affectedGuaranteeId;
        }
    }
}
