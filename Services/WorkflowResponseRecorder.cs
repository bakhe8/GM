using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowResponseRecorder
    {
        private readonly IDatabaseService _databaseService;
        private readonly WorkflowResponseStorageService _responseStorage;

        public WorkflowResponseRecorder(IDatabaseService databaseService, WorkflowResponseStorageService responseStorage)
        {
            _databaseService = databaseService;
            _responseStorage = responseStorage;
        }

        public void RecordBankResponse(
            int requestId,
            RequestStatus resultStatus,
            string responseNotes,
            string? responseDocumentPath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            using var scope = SimpleLogger.BeginScope("Workflow.RecordBankResponse");
            if (resultStatus == RequestStatus.Pending)
            {
                throw new InvalidOperationException("لا يمكن إبقاء الطلب في حالة انتظار عند تسجيل رد البنك.");
            }

            WorkflowRequest request = _databaseService.GetWorkflowRequestById(requestId)
                ?? throw new InvalidOperationException("الطلب المطلوب غير موجود.");

            if (request.Status != RequestStatus.Pending)
            {
                throw new InvalidOperationException("هذا الطلب أُغلق سابقًا ولا يمكن تسجيل نتيجة جديدة له.");
            }

            if (promoteResponseDocumentToOfficialAttachment && request.Type != RequestType.Verification)
            {
                throw new InvalidOperationException("إضافة مستند رد البنك كمرفق رسمي متاحة لطلبات التحقق فقط في هذه المرحلة.");
            }

            if (promoteResponseDocumentToOfficialAttachment && resultStatus != RequestStatus.Executed)
            {
                throw new InvalidOperationException("يمكن ترقية مستند رد البنك إلى مرفق رسمي فقط عند تسجيل نتيجة منفذة.");
            }

            string normalizedResponseNotes = responseNotes?.Trim() ?? string.Empty;
            string responseOriginalFileName = string.Empty;
            string responseSavedFileName = string.Empty;
            StagedWorkflowResponseDocument? stagedResponseDocument = null;

            if (!string.IsNullOrWhiteSpace(responseDocumentPath))
            {
                stagedResponseDocument = _responseStorage.StageCopy(responseDocumentPath);
                responseOriginalFileName = stagedResponseDocument.OriginalFileName;
                responseSavedFileName = stagedResponseDocument.SavedFileName;
            }

            bool databaseCommitted = false;
            try
            {
                if (resultStatus == RequestStatus.Executed)
                {
                    try
                    {
                        ExecuteApprovedRequest(
                            request,
                            normalizedResponseNotes,
                            responseOriginalFileName,
                            responseSavedFileName,
                            responseDocumentPath,
                            promoteResponseDocumentToOfficialAttachment);
                        databaseCommitted = true;
                    }
                    catch (DeferredFilePromotionException)
                    {
                        databaseCommitted = true;
                        throw;
                    }

                    if (stagedResponseDocument != null)
                    {
                        _responseStorage.FinalizeStagedCopy(stagedResponseDocument, "RecordBankResponse");
                    }

                    return;
                }

                _databaseService.RecordWorkflowResponse(
                    request.Id,
                    resultStatus,
                    normalizedResponseNotes,
                    responseOriginalFileName,
                    responseSavedFileName,
                    null);
                databaseCommitted = true;

                if (stagedResponseDocument != null)
                {
                    _responseStorage.FinalizeStagedCopy(stagedResponseDocument, "RecordBankResponse");
                }
            }
            catch (Exception ex)
            {
                if (!databaseCommitted)
                {
                    _responseStorage.CleanupStagedCopy(stagedResponseDocument);
                    _responseStorage.DeleteCommittedFile(responseSavedFileName);
                }

                throw OperationFailure.LogAndWrap(
                    ex,
                    "Workflow.RecordBankResponse",
                    "تعذر تسجيل رد البنك على الطلب الحالي.");
            }
        }

        public void AttachResponseDocumentToClosedRequest(
            int requestId,
            string responseDocumentPath,
            string additionalNotes = "")
        {
            using var scope = SimpleLogger.BeginScope("Workflow.AttachResponseDocumentToClosedRequest");

            if (string.IsNullOrWhiteSpace(responseDocumentPath))
            {
                throw new InvalidOperationException("يرجى اختيار مستند رد البنك أولًا.");
            }

            WorkflowRequest request = _databaseService.GetWorkflowRequestById(requestId)
                ?? throw new InvalidOperationException("الطلب المطلوب غير موجود.");

            if (request.Status == RequestStatus.Pending)
            {
                throw new InvalidOperationException("لا يمكن إلحاق مستند الرد بطلب ما زال معلقًا. استخدم شاشة تسجيل رد البنك أولًا.");
            }

            if (request.HasResponseDocument)
            {
                throw new InvalidOperationException("يوجد بالفعل مستند رد محفوظ لهذا الطلب.");
            }

            string normalizedAdditionalNotes = additionalNotes?.Trim() ?? string.Empty;
            string mergedResponseNotes = string.IsNullOrWhiteSpace(normalizedAdditionalNotes)
                ? request.ResponseNotes
                : WorkflowExecutionDataAccess.AppendNote(request.ResponseNotes, normalizedAdditionalNotes);

            StagedWorkflowResponseDocument stagedResponseDocument = _responseStorage.StageCopy(responseDocumentPath);
            bool databaseCommitted = false;

            try
            {
                _databaseService.AttachWorkflowResponseDocument(
                    request.Id,
                    mergedResponseNotes,
                    stagedResponseDocument.OriginalFileName,
                    stagedResponseDocument.SavedFileName);
                databaseCommitted = true;
                _responseStorage.FinalizeStagedCopy(stagedResponseDocument, "AttachResponseDocumentToClosedRequest");
            }
            catch (Exception ex)
            {
                if (!databaseCommitted)
                {
                    _responseStorage.CleanupStagedCopy(stagedResponseDocument);
                    _responseStorage.DeleteCommittedFile(stagedResponseDocument.SavedFileName);
                }

                throw OperationFailure.LogAndWrap(
                    ex,
                    "Workflow.AttachResponseDocumentToClosedRequest",
                    "تعذر إلحاق مستند رد البنك بالطلب الحالي.");
            }
        }

        private void ExecuteApprovedRequest(
            WorkflowRequest request,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseDocumentPath,
            bool promoteResponseDocumentToOfficialAttachment)
        {
            switch (request.Type)
            {
                case RequestType.Extension:
                    _databaseService.ExecuteExtensionWorkflowRequest(
                        request.Id,
                        request.RequestedExpiryDate ?? throw new InvalidOperationException("تاريخ التمديد المطلوب غير موجود داخل الطلب."),
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath);
                    return;

                case RequestType.Reduction:
                    _databaseService.ExecuteReductionWorkflowRequest(
                        request.Id,
                        request.RequestedAmount ?? throw new InvalidOperationException("المبلغ المطلوب بعد التخفيض غير موجود داخل الطلب."),
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath);
                    return;

                case RequestType.Release:
                    _databaseService.ExecuteReleaseWorkflowRequest(
                        request.Id,
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath);
                    return;

                case RequestType.Liquidation:
                    _databaseService.ExecuteLiquidationWorkflowRequest(
                        request.Id,
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath);
                    return;

                case RequestType.Verification:
                    _databaseService.ExecuteVerificationWorkflowRequest(
                        request.Id,
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath,
                        promoteResponseDocumentToOfficialAttachment);
                    return;

                case RequestType.Replacement:
                    _databaseService.ExecuteReplacementWorkflowRequest(
                        request.Id,
                        request.ReplacementGuaranteeNo,
                        request.ReplacementSupplier,
                        request.ReplacementBank,
                        request.ReplacementAmount ?? throw new InvalidOperationException("مبلغ الضمان البديل غير موجود داخل الطلب."),
                        request.ReplacementExpiryDate ?? throw new InvalidOperationException("تاريخ انتهاء الضمان البديل غير موجود داخل الطلب."),
                        request.ReplacementGuaranteeType,
                        request.ReplacementBeneficiary,
                        request.ReplacementReferenceType,
                        request.ReplacementReferenceNumber,
                        responseNotes,
                        responseOriginalFileName,
                        responseSavedFileName,
                        responseDocumentPath);
                    return;

                default:
                    throw new NotSupportedException("هذه الشريحة تدعم تنفيذ التمديد والتخفيض والإفراج والتسييل والتحقق والاستبدال فقط في الوقت الحالي.");
            }
        }
    }
}
