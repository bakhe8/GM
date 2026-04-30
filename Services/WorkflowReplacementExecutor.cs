using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowReplacementExecutor
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;

        public WorkflowReplacementExecutor(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
        }

        public int Execute(
            int requestId,
            string replacementGuaranteeNo,
            string replacementSupplier,
            string replacementBank,
            decimal replacementAmount,
            DateTime replacementExpiryDate,
            string replacementGuaranteeType,
            string replacementBeneficiary,
            GuaranteeReferenceType replacementReferenceType,
            string replacementReferenceNumber,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            List<StagedAttachmentFile> stagedResponseAttachments = _attachmentStorage.StageCopies(
                string.IsNullOrWhiteSpace(responseAttachmentSourcePath)
                    ? Array.Empty<string>()
                    : new[] { responseAttachmentSourcePath });
            int newGuaranteeId = 0;
            GuaranteeReferenceType normalizedReferenceType = replacementReferenceType;
            string normalizedReferenceNumber = replacementReferenceNumber?.Trim() ?? string.Empty;

            using var connection = SqliteConnectionFactory.Open(_connectionString);

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    DateTime executedAt = DateTime.Now;
                    WorkflowExecutionContext context = WorkflowExecutionDataAccess.LoadContext(
                        requestId,
                        RequestType.Replacement,
                        "نوع الطلب لا يطابق طريقة تنفيذ الاستبدال.",
                        "لا يمكن تنفيذ طلب استبدال غير معلق.",
                        connection,
                        transaction);

                    if (string.IsNullOrWhiteSpace(replacementGuaranteeNo))
                    {
                        throw new InvalidOperationException("رقم الضمان البديل مطلوب لتنفيذ الاستبدال.");
                    }

                    if (replacementAmount <= 0)
                    {
                        throw new InvalidOperationException("مبلغ الضمان البديل يجب أن يكون أكبر من صفر.");
                    }

                    if (replacementExpiryDate == DateTime.MinValue)
                    {
                        throw new InvalidOperationException("تاريخ انتهاء الضمان البديل غير صالح.");
                    }

                    Guarantee? conflictingGuarantee = WorkflowExecutionDataAccess.GetCurrentGuaranteeByNo(
                        replacementGuaranteeNo,
                        connection,
                        transaction);
                    if (conflictingGuarantee != null)
                    {
                        throw new InvalidOperationException("رقم الضمان البديل مستخدم حاليًا في سجل آخر. يرجى استخدام رقم ضمان مختلف.");
                    }

                    string replacementExecutionNote =
                        $"نتيجة تنفيذ طلب استبدال رقم {context.Request.SequenceNumber} للضمان {context.CurrentGuarantee.GuaranteeNo} بتاريخ {executedAt:yyyy-MM-dd}.";
                    newGuaranteeId = WorkflowExecutionDataAccess.InsertStandaloneGuarantee(
                        replacementSupplier,
                        replacementBank,
                        replacementGuaranteeNo.Trim(),
                        replacementAmount,
                        replacementExpiryDate,
                        replacementGuaranteeType,
                        replacementBeneficiary,
                        replacementExecutionNote,
                        normalizedReferenceType,
                        normalizedReferenceNumber,
                        GuaranteeLifecycleStatus.Active,
                        context.Request.RootGuaranteeId,
                        executedAt,
                        connection,
                        transaction);
                    WorkflowExecutionDataAccess.SetGuaranteeRootIdToSelf(newGuaranteeId, connection, transaction);

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

                    string replacedGuaranteeNote =
                        WorkflowExecutionDataAccess.AppendNote(
                            context.CurrentGuarantee.Notes,
                            $"تم استبداله بالضمان رقم {replacementGuaranteeNo.Trim()} بتاريخ {executedAt:yyyy-MM-dd}.");
                    WorkflowExecutionDataAccess.UpdateGuaranteeLifecycleStatus(
                        context.CurrentGuarantee.Id,
                        GuaranteeLifecycleStatus.Replaced,
                        replacedGuaranteeNote,
                        newGuaranteeId,
                        connection,
                        transaction);

                    WorkflowExecutionDataAccess.UpdateWorkflowRequestAsExecuted(
                        requestId,
                        responseNotes ?? string.Empty,
                        responseOriginalFileName,
                        responseSavedFileName,
                        newGuaranteeId,
                        executedAt,
                        connection,
                        transaction);

                    WorkflowExecutionDataAccess.SupersedePendingRequests(
                        context.Request.RootGuaranteeId,
                        requestId,
                        "أُسقط الطلب تلقائيًا بسبب تنفيذ طلب الاستبدال.",
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

            _attachmentStorage.FinalizeStagedCopies(stagedResponseAttachments, "ExecuteReplacementWorkflowRequest");
            return newGuaranteeId;
        }
    }
}
