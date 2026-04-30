using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class RequestsWorkspaceCoordinator
    {
        private readonly IDatabaseService _database;
        private readonly IWorkflowService _workflow;
        private readonly IShellStatusService _shellStatus;
        private readonly Action<int> _onChanged;

        public RequestsWorkspaceCoordinator(
            IDatabaseService database,
            IWorkflowService workflow,
            IShellStatusService shellStatus,
            Action<int>? onChanged)
        {
            _database = database;
            _workflow = workflow;
            _shellStatus = shellStatus;
            _onChanged = onChanged ?? (_ => { });
        }

        public void RegisterSelectedResponse(RequestListDisplayItem? selectedItem, Action<int?> reloadRequests)
        {
            if (selectedItem?.Item.Request is not { Status: RequestStatus.Pending } request)
            {
                MessageBox.Show("اختر طلباً معلقاً أولاً.", "الطلبات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BankResponseDialog.TryShow(new[] { request }, out int requestId, out RequestStatus status, out string notes, out string responsePath))
            {
                return;
            }

            int rootId = selectedItem.Item.RootGuaranteeId;
            try
            {
                _workflow.RecordBankResponse(requestId, status, notes, string.IsNullOrWhiteSpace(responsePath) ? null : responsePath);
                _onChanged(rootId);
                reloadRequests(requestId);
                _shellStatus.ShowSuccess("تم تسجيل رد البنك.", $"الطلبات • {selectedItem.Item.GuaranteeNo}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "الطلبات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void HandleResponseAction(RequestListDisplayItem? selectedItem, Action<int?> reloadRequests)
        {
            if (selectedItem == null)
            {
                MessageBox.Show("اختر طلبًا أولًا.", "الطلبات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            WorkflowRequest request = selectedItem.Item.Request;
            if (request.HasResponseDocument)
            {
                OpenResponse(selectedItem);
                return;
            }

            if (request.Status == RequestStatus.Pending)
            {
                RegisterSelectedResponse(selectedItem, reloadRequests);
                return;
            }

            AttachResponseDocument(selectedItem, reloadRequests);
        }

        public void OpenLetter(RequestListDisplayItem? selectedItem)
        {
            WorkflowRequest? request = selectedItem?.Item.Request;
            if (request is { HasLetter: true })
            {
                TryOpenWorkflowDocument(
                    () => _workflow.OpenRequestLetter(request),
                    "خطاب الطلب",
                    "تعذر فتح خطاب الطلب. الملف غير موجود.",
                    () => _shellStatus.ShowInfo("تم فتح خطاب الطلب.", $"الطلبات • {selectedItem?.Item.GuaranteeNo ?? "---"}"));
            }
        }

        public void OpenResponse(RequestListDisplayItem? selectedItem)
        {
            WorkflowRequest? request = selectedItem?.Item.Request;
            if (request is { HasResponseDocument: true })
            {
                TryOpenWorkflowDocument(
                    () => _workflow.OpenResponseDocument(request),
                    "رد البنك",
                    "تعذر فتح رد البنك. الملف غير موجود.",
                    () => _shellStatus.ShowInfo("تم فتح رد البنك.", $"الطلبات • {selectedItem?.Item.GuaranteeNo ?? "---"}"));
            }
        }

        public void CreateExtensionFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب تمديد",
                "اختر ضمانًا نشطًا مؤهلًا، ثم حدّد تاريخ الانتهاء المطلوب.",
                _workflow.GetGuaranteesEligibleForExtension());
            if (guarantee == null)
            {
                return;
            }

            DateTime suggestedDate = ExtensionRequestFlowSupport.GetSuggestedRequestedExpiryDate(guarantee);
            if (!GuidedTextPromptDialog.TryShow(
                    "طلب تمديد",
                    $"أدخل تاريخ الانتهاء المطلوب للضمان {guarantee.GuaranteeNo}.",
                    "تاريخ الانتهاء المطلوب",
                    "إنشاء الطلب",
                    suggestedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    out string requestedDateText,
                    "مثال: 2026/12/31"))
            {
                return;
            }

            if (!DateTime.TryParse(requestedDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime requestedDate))
            {
                MessageBox.Show("صيغة التاريخ غير صحيحة. استخدم مثلاً 2026/12/31.", "طلب تمديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ExtensionRequestFlowSupport.TryValidate(guarantee, requestedDate, Environment.UserName, out string validationReason))
            {
                MessageBox.Show(validationReason, "طلب تمديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string notes = $"طلب تمديد من مركز الطلبات. {ExtensionRequestFlowSupport.BuildReasonSummary(guarantee)}";
            ExecuteEligibleRequest(
                "طلب تمديد",
                guarantee,
                reloadRequests,
                () => _workflow.CreateExtensionRequest(guarantee.Id, requestedDate, notes, Environment.UserName),
                $"تم إنشاء طلب تمديد للضمان {guarantee.GuaranteeNo}.");
        }

        private static void TryOpenWorkflowDocument(Action openAction, string title, string missingMessage, Action? onSuccess = null)
        {
            try
            {
                openAction();
                onSuccess?.Invoke();
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show(missingMessage, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void CreateReductionFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب تخفيض",
                "اختر ضمانًا مؤهلًا ثم أدخل المبلغ المطلوب بعد التخفيض.",
                _workflow.GetGuaranteesEligibleForReduction());
            if (guarantee == null)
            {
                return;
            }

            if (!GuidedTextPromptDialog.TryShow(
                    "طلب تخفيض",
                    $"أدخل المبلغ المطلوب بعد التخفيض للضمان {guarantee.GuaranteeNo}.",
                    "المبلغ المطلوب",
                    "إنشاء الطلب",
                    guarantee.Amount.ToString("N0", CultureInfo.InvariantCulture),
                    out string amountText))
            {
                return;
            }

            string normalizedAmount = amountText.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            if (!decimal.TryParse(normalizedAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal requestedAmount))
            {
                MessageBox.Show("صيغة المبلغ غير صحيحة.", "طلب تخفيض", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExecuteEligibleRequest(
                "طلب تخفيض",
                guarantee,
                reloadRequests,
                () => _workflow.CreateReductionRequest(guarantee.Id, requestedAmount, "طلب تخفيض من مركز الطلبات", Environment.UserName),
                $"تم إنشاء طلب تخفيض للضمان {guarantee.GuaranteeNo}.");
        }

        public void CreateReleaseFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب إفراج",
                "اختر ضمانًا نشطًا مؤهلًا لإرسال طلب الإفراج.",
                _workflow.GetGuaranteesEligibleForRelease());
            if (guarantee == null)
            {
                return;
            }

            if (MessageBox.Show(
                    $"تأكيد إنشاء طلب إفراج للضمان {guarantee.GuaranteeNo}؟",
                    "طلب إفراج",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            ExecuteEligibleRequest(
                "طلب إفراج",
                guarantee,
                reloadRequests,
                () => _workflow.CreateReleaseRequest(guarantee.Id, "طلب إفراج من مركز الطلبات", Environment.UserName),
                $"تم إنشاء طلب إفراج للضمان {guarantee.GuaranteeNo}.");
        }

        public void CreateLiquidationFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب تسييل",
                "اختر ضمانًا مؤهلًا ثم أدخل سبب أو ملاحظات طلب التسييل.",
                _workflow.GetGuaranteesEligibleForLiquidation());
            if (guarantee == null)
            {
                return;
            }

            if (!GuidedTextPromptDialog.TryShow(
                    "طلب تسييل",
                    $"أدخل ملاحظات طلب التسييل للضمان {guarantee.GuaranteeNo}.",
                    "ملاحظات الطلب",
                    "إنشاء الطلب",
                    "طلب تسييل من مركز الطلبات.",
                    out string notes))
            {
                return;
            }

            string normalizedNotes = string.IsNullOrWhiteSpace(notes) ? "طلب تسييل من مركز الطلبات." : notes.Trim();
            ExecuteEligibleRequest(
                "طلب تسييل",
                guarantee,
                reloadRequests,
                () => _workflow.CreateLiquidationRequest(guarantee.Id, normalizedNotes, Environment.UserName),
                $"تم إنشاء طلب تسييل للضمان {guarantee.GuaranteeNo}.");
        }

        public void CreateVerificationFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب تحقق",
                "اختر ضمانًا مؤهلًا ثم أدخل ملاحظات طلب التحقق.",
                _workflow.GetGuaranteesEligibleForVerification());
            if (guarantee == null)
            {
                return;
            }

            if (!GuidedTextPromptDialog.TryShow(
                    "طلب تحقق",
                    $"أدخل ملاحظات طلب التحقق للضمان {guarantee.GuaranteeNo}.",
                    "ملاحظات الطلب",
                    "إنشاء الطلب",
                    "طلب تحقق من مركز الطلبات.",
                    out string notes))
            {
                return;
            }

            string normalizedNotes = string.IsNullOrWhiteSpace(notes) ? "طلب تحقق من مركز الطلبات." : notes.Trim();
            ExecuteEligibleRequest(
                "طلب تحقق",
                guarantee,
                reloadRequests,
                () => _workflow.CreateVerificationRequest(guarantee.Id, normalizedNotes, Environment.UserName),
                $"تم إنشاء طلب تحقق للضمان {guarantee.GuaranteeNo}.");
        }

        public void CreateReplacementFromEligible(Action<int?> reloadRequests)
        {
            Guarantee? guarantee = SelectEligibleGuarantee(
                "طلب استبدال",
                "اختر الضمان المؤهل أولًا، ثم أكمل بيانات الضمان البديل.",
                _workflow.GetGuaranteesEligibleForReplacement());
            if (guarantee == null)
            {
                return;
            }

            if (!ReplacementRequestDialog.TryShow(
                    guarantee,
                    _database.GetUniqueValues("Bank"),
                    _database.GetUniqueValues("GuaranteeType"),
                    _database.IsGuaranteeNoUnique,
                    out ReplacementRequestInput input))
            {
                return;
            }

            ExecuteEligibleRequest(
                "طلب استبدال",
                guarantee,
                reloadRequests,
                () => _workflow.CreateReplacementRequest(
                    guarantee.Id,
                    input.ReplacementGuaranteeNo,
                    input.ReplacementSupplier,
                    input.ReplacementBank,
                    input.ReplacementAmount,
                    input.ReplacementExpiryDate,
                    input.ReplacementGuaranteeType,
                    input.ReplacementBeneficiary,
                    input.ReplacementReferenceType,
                    input.ReplacementReferenceNumber,
                    input.Notes,
                    Environment.UserName),
                $"تم إنشاء طلب استبدال للضمان {guarantee.GuaranteeNo}.");
        }

        public void CopyGuaranteeNo(RequestListDisplayItem? selectedItem)
        {
            CopyText(selectedItem?.Item.GuaranteeNo, "رقم الضمان");
        }

        public void CopySupplier(RequestListDisplayItem? selectedItem)
        {
            CopyText(selectedItem?.Item.Supplier, "اسم المورد");
        }

        private void AttachResponseDocument(RequestListDisplayItem selectedItem, Action<int?> reloadRequests)
        {
            if (!selectedItem.CanAttachResponseDocument)
            {
                MessageBox.Show("تعذر استخدام إجراء مستند الرد لهذا الطلب.", "الطلبات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!AttachResponseDocumentDialog.TryShow(selectedItem.Item, out string responsePath, out string additionalNotes))
            {
                return;
            }

            int rootId = selectedItem.Item.RootGuaranteeId;
            try
            {
                _workflow.AttachResponseDocumentToClosedRequest(selectedItem.Item.Request.Id, responsePath, additionalNotes);
                _onChanged(rootId);
                reloadRequests(selectedItem.Item.Request.Id);
                _shellStatus.ShowSuccess("تم إلحاق مستند رد البنك.", $"الطلبات • {selectedItem.Item.GuaranteeNo}");
            }
            catch (DeferredFilePromotionException ex)
            {
                _onChanged(rootId);
                reloadRequests(selectedItem.Item.Request.Id);
                MessageBox.Show(ex.UserMessage, "الطلبات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "الطلبات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Guarantee? SelectEligibleGuarantee(string title, string description, IReadOnlyList<Guarantee> guarantees)
        {
            if (guarantees.Count == 0)
            {
                MessageBox.Show("لا توجد ضمانات مؤهلة حاليًا لهذا الإجراء.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            if (!EligibleGuaranteePickerDialog.TryShow(title, description, guarantees, out Guarantee? guarantee) || guarantee == null)
            {
                return null;
            }

            return guarantee;
        }

        private void ExecuteEligibleRequest(
            string title,
            Guarantee guarantee,
            Action<int?> reloadRequests,
            Func<WorkflowRequest> action,
            string successMessage)
        {
            try
            {
                WorkflowRequest request = action();
                int rootId = guarantee.RootId ?? guarantee.Id;
                _onChanged(rootId);
                reloadRequests(request.Id);
                _shellStatus.ShowSuccess(successMessage, $"الطلبات • {guarantee.GuaranteeNo}");
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyText(string? text, string label)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "---")
            {
                MessageBox.Show($"لا توجد قيمة متاحة لنسخ {label}.", $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(text);
                _shellStatus.ShowInfo(
                    $"تم نسخ {label}.",
                    "الحافظة جاهزة للاستخدام");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
