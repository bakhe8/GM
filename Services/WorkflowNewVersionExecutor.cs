using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowNewVersionExecutor
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;

        public WorkflowNewVersionExecutor(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
        }

        public int Execute(
            int requestId,
            RequestType expectedType,
            DateTime? newExpiryDate,
            decimal? newAmount,
            GuaranteeLifecycleStatus? newLifecycleStatus,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath,
            bool cancelOtherPendingRequests,
            string? cancelOtherPendingRequestsNote = null)
        {
            if (newAmount.HasValue)
            {
                ArabicAmountFormatter.EnsurePositiveSaudiRiyalAmount(newAmount.Value, "المبلغ الناتج");
                newAmount = ArabicAmountFormatter.NormalizeSaudiRiyalAmount(newAmount.Value);
            }

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
                        expectedType,
                        "نوع الطلب لا يطابق طريقة التنفيذ المطلوبة.",
                        "لا يمكن تنفيذ طلب غير معلق.",
                        connection,
                        transaction);

                    string effectiveResponseNotes = responseNotes ?? string.Empty;
                    string mismatchNote = WorkflowExecutionDataAccess.DetectReductionMismatch(context.Request, newAmount);
                    if (!string.IsNullOrEmpty(mismatchNote))
                    {
                        effectiveResponseNotes = string.IsNullOrWhiteSpace(effectiveResponseNotes)
                            ? mismatchNote
                            : $"{mismatchNote}{Environment.NewLine}{effectiveResponseNotes}";
                    }

                    int nextVersionNumber = WorkflowExecutionDataAccess.GetNextVersionNumber(
                        context.Request.RootGuaranteeId,
                        connection,
                        transaction);
                    WorkflowExecutionDataAccess.ClearCurrentGuaranteeFlag(
                        context.Request.RootGuaranteeId,
                        connection,
                        transaction);

                    string executionNote = $"تم تنفيذ {context.Request.TypeLabel} رقم {context.Request.SequenceNumber} بتاريخ {DualCalendarDateService.FormatGregorianDate(executedAt)}.";
                    newGuaranteeId = WorkflowExecutionDataAccess.InsertGuaranteeVersion(
                        context.CurrentGuarantee,
                        context.Request.RootGuaranteeId,
                        nextVersionNumber,
                        newAmount ?? context.CurrentGuarantee.Amount,
                        newExpiryDate ?? context.CurrentGuarantee.ExpiryDate,
                        WorkflowExecutionDataAccess.AppendNote(context.CurrentGuarantee.Notes, executionNote),
                        newLifecycleStatus ?? context.CurrentGuarantee.LifecycleStatus,
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
                            transaction,
                            AttachmentDocumentType.BankResponse);
                    }

                    WorkflowExecutionDataAccess.UpdateWorkflowRequestAsExecuted(
                        requestId,
                        effectiveResponseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        newGuaranteeId,
                        executedAt,
                        connection,
                        transaction);

                    if (cancelOtherPendingRequests)
                    {
                        WorkflowExecutionDataAccess.SupersedePendingRequests(
                            context.Request.RootGuaranteeId,
                            requestId,
                            cancelOtherPendingRequestsNote ?? "أُسقط الطلب تلقائيًا بسبب تنفيذ طلب أحدث.",
                            executedAt,
                            connection,
                            transaction);
                    }

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
                $"NewVersionId={newGuaranteeId}");
            return newGuaranteeId;
        }
    }
}
