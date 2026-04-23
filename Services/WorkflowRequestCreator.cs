using System;
using System.IO;
using System.Text.Json;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowRequestCreator
    {
        private readonly IDatabaseService _databaseService;
        private readonly WorkflowLetterService _letterService;

        public WorkflowRequestCreator(IDatabaseService databaseService, WorkflowLetterService letterService)
        {
            _databaseService = databaseService;
            _letterService = letterService;
        }

        public WorkflowRequest CreateAnnulmentRequest(int guaranteeId, string reason, string createdBy = "")
        {
            using var scope = SimpleLogger.BeginScope("Workflow.CreateAnnulmentRequest");
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Released &&
                guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Liquidated)
            {
                throw new InvalidOperationException($"لا يمكن إنشاء طلب نقض لضمان في حالة {guarantee.LifecycleStatusLabel}. النقض متاح للضمانات المفرج عنها أو المسيّلة فقط.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Annulment))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب نقض معلق لهذا الضمان.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateAnnulmentLetter(guarantee, reason, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Annulment,
                new WorkflowRequestedData(),
                generatedLetter,
                reason,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateExtensionRequest(int guaranteeId, DateTime requestedExpiryDate, string notes, string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب تمديد لضمان غير نشط.");
            }

            if (requestedExpiryDate.Date <= guarantee.ExpiryDate.Date)
            {
                throw new InvalidOperationException("تاريخ التمديد المطلوب يجب أن يكون بعد تاريخ الانتهاء الحالي.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Extension))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب تمديد معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateExtensionLetter(guarantee, requestedExpiryDate, notes, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Extension,
                new WorkflowRequestedData
                {
                    RequestedExpiryDate = requestedExpiryDate.Date
                },
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateReductionRequest(int guaranteeId, decimal requestedAmount, string notes, string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب تخفيض لضمان غير نشط.");
            }

            if (requestedAmount <= 0 || requestedAmount >= guarantee.Amount)
            {
                throw new InvalidOperationException("المبلغ المطلوب بعد التخفيض يجب أن يكون أكبر من صفر وأقل من المبلغ الحالي.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Reduction))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب تخفيض معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateReductionLetter(guarantee, requestedAmount, notes, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Reduction,
                new WorkflowRequestedData
                {
                    RequestedAmount = requestedAmount
                },
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateReleaseRequest(int guaranteeId, string notes, string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب إفراج لضمان غير نشط.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Release))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب إفراج معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateReleaseLetter(guarantee, notes, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Release,
                new WorkflowRequestedData(),
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateLiquidationRequest(int guaranteeId, string notes, string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب تسييل لضمان غير نشط.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Liquidation))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب تسييل معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateLiquidationLetter(guarantee, notes, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Liquidation,
                new WorkflowRequestedData(),
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateVerificationRequest(int guaranteeId, string notes, string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب تحقق لضمان غير نشط.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Verification))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب تحقق معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            GeneratedWorkflowFile generatedLetter = _letterService.CreateVerificationLetter(guarantee, notes, normalizedCreatedBy);
            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Verification,
                new WorkflowRequestedData(),
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        public WorkflowRequest CreateReplacementRequest(
            int guaranteeId,
            string replacementGuaranteeNo,
            string replacementSupplier,
            string replacementBank,
            decimal replacementAmount,
            DateTime replacementExpiryDate,
            string replacementGuaranteeType,
            string replacementBeneficiary,
            GuaranteeReferenceType replacementReferenceType,
            string replacementReferenceNumber,
            string notes,
            string createdBy = "")
        {
            Guarantee guarantee = ResolveCurrentGuarantee(guaranteeId);
            int rootId = guarantee.RootId ?? guarantee.Id;
            string normalizedCreatedBy = WorkflowCreatedByPolicy.NormalizeForNewRequest(createdBy);

            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                throw new InvalidOperationException("لا يمكن إنشاء طلب استبدال لضمان غير نشط.");
            }

            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Replacement))
            {
                throw new InvalidOperationException("يوجد بالفعل طلب استبدال معلق لهذا الضمان. يمكنك إعادة طباعة الخطاب الحالي بدل إنشاء طلب جديد.");
            }

            if (string.IsNullOrWhiteSpace(replacementGuaranteeNo))
            {
                throw new InvalidOperationException("رقم الضمان البديل مطلوب.");
            }

            if (!_databaseService.IsGuaranteeNoUnique(replacementGuaranteeNo.Trim()))
            {
                throw new InvalidOperationException("رقم الضمان البديل مستخدم حاليًا في سجل آخر. يرجى اختيار رقم مختلف.");
            }

            if (replacementAmount <= 0)
            {
                throw new InvalidOperationException("مبلغ الضمان البديل يجب أن يكون أكبر من صفر.");
            }

            if (replacementExpiryDate.Date == DateTime.MinValue.Date)
            {
                throw new InvalidOperationException("تاريخ انتهاء الضمان البديل غير صالح.");
            }

            GuaranteeReferenceType normalizedReferenceType = replacementReferenceType == GuaranteeReferenceType.None
                ? guarantee.ReferenceType
                : replacementReferenceType;
            string normalizedReferenceNumber = string.IsNullOrWhiteSpace(replacementReferenceNumber)
                ? guarantee.ReferenceNumber
                : replacementReferenceNumber.Trim();

            GeneratedWorkflowFile generatedLetter = _letterService.CreateReplacementLetter(
                guarantee,
                replacementGuaranteeNo.Trim(),
                replacementAmount,
                replacementExpiryDate.Date,
                notes,
                normalizedCreatedBy);

            return SaveLetterBackedRequest(
                rootId,
                guarantee.Id,
                RequestType.Replacement,
                new WorkflowRequestedData
                {
                    ReplacementGuaranteeNo = replacementGuaranteeNo.Trim(),
                    ReplacementSupplier = replacementSupplier?.Trim() ?? guarantee.Supplier,
                    ReplacementBank = replacementBank?.Trim() ?? guarantee.Bank,
                    ReplacementAmount = replacementAmount,
                    ReplacementExpiryDate = replacementExpiryDate.Date,
                    ReplacementGuaranteeType = replacementGuaranteeType?.Trim() ?? guarantee.GuaranteeType,
                    ReplacementBeneficiary = replacementBeneficiary?.Trim() ?? guarantee.Beneficiary,
                    ReplacementReferenceType = normalizedReferenceType,
                    ReplacementReferenceNumber = normalizedReferenceNumber
                },
                generatedLetter,
                notes,
                normalizedCreatedBy);
        }

        private WorkflowRequest SaveLetterBackedRequest(
            int rootId,
            int baseVersionId,
            RequestType requestType,
            WorkflowRequestedData requestedData,
            GeneratedWorkflowFile generatedLetter,
            string notes,
            string normalizedCreatedBy)
        {
            try
            {
                WorkflowRequest request = CreatePendingRequest(
                    rootId,
                    baseVersionId,
                    requestType,
                    requestedData,
                    notes,
                    normalizedCreatedBy);
                request.LetterOriginalFileName = generatedLetter.OriginalFileName;
                request.LetterSavedFileName = generatedLetter.SavedFileName;
                request.Id = _databaseService.SaveWorkflowRequest(request);
                return request;
            }
            catch (Exception ex)
            {
                TryDeleteFile(generatedLetter.FullPath);
                throw OperationFailure.LogAndWrap(
                    ex,
                    $"Workflow.Create{requestType}Request",
                    "تعذر إنشاء الطلب الحالي.");
            }
        }

        private static WorkflowRequest CreatePendingRequest(
            int rootId,
            int baseVersionId,
            RequestType requestType,
            WorkflowRequestedData requestedData,
            string notes,
            string createdBy)
        {
            DateTime now = DateTime.Now;
            return new WorkflowRequest
            {
                RootGuaranteeId = rootId,
                BaseVersionId = baseVersionId,
                Type = requestType,
                Status = RequestStatus.Pending,
                RequestDate = now,
                CreatedAt = now,
                UpdatedAt = now,
                RequestedDataJson = JsonSerializer.Serialize(requestedData),
                Notes = notes?.Trim() ?? string.Empty,
                CreatedBy = createdBy
            };
        }

        private Guarantee ResolveCurrentGuarantee(int guaranteeId)
        {
            Guarantee guarantee = _databaseService.GetGuaranteeById(guaranteeId)
                ?? throw new InvalidOperationException("تعذر العثور على الضمان المحدد.");

            int rootId = guarantee.RootId ?? guarantee.Id;
            return _databaseService.GetCurrentGuaranteeByRootId(rootId)
                ?? throw new InvalidOperationException("تعذر تحميل النسخة الحالية للضمان المحدد.");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Warning: Cleanup failed for {path}: {ex.Message}", "WARNING");
            }
        }
    }
}
