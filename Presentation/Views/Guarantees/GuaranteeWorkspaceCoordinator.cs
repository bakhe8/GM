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
    public sealed class GuaranteeWorkspaceCoordinator
    {
        private readonly IDatabaseService _database;
        private readonly IWorkflowService _workflow;
        private readonly IExcelService _excel;
        private readonly IOperationalInquiryService _inquiry;
        private readonly IShellStatusService _shellStatus;
        private readonly IUiDiagnosticsService _diagnostics;
        private readonly Action _loadFilterOptions;
        private readonly Action<int, int?> _refreshAfterWorkflowChange;

        public GuaranteeWorkspaceCoordinator(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            IOperationalInquiryService inquiry,
            IShellStatusService shellStatus,
            Action loadFilterOptions,
            Action<int, int?> refreshAfterWorkflowChange)
        {
            _database = database;
            _workflow = workflow;
            _excel = excel;
            _inquiry = inquiry;
            _shellStatus = shellStatus;
            _diagnostics = App.CurrentApp.GetRequiredService<IUiDiagnosticsService>();
            _loadFilterOptions = loadFilterOptions;
            _refreshAfterWorkflowChange = refreshAfterWorkflowChange;
        }

        public void CreateNewGuarantee()
        {
            if (!NewGuaranteeDialog.TryShow(
                    _database.GetUniqueValues("Bank"),
                    _database.GetUniqueValues("GuaranteeType"),
                    _database.IsGuaranteeNoUnique,
                    out NewGuaranteeInput input))
            {
                return;
            }

            ExecuteAction("إضافة ضمان", () =>
            {
                var guarantee = new Guarantee
                {
                    Supplier = input.Supplier,
                    Bank = input.Bank,
                    GuaranteeNo = input.GuaranteeNo,
                    Amount = input.Amount,
                    ExpiryDate = input.ExpiryDate,
                    GuaranteeType = input.GuaranteeType,
                    Beneficiary = input.Beneficiary,
                    ReferenceType = input.ReferenceType,
                    ReferenceNumber = input.ReferenceNumber,
                    Notes = input.Notes,
                    CreatedAt = DateTime.Now,
                    LifecycleStatus = GuaranteeLifecycleStatus.Active
                };

                _database.SaveGuaranteeWithAttachments(guarantee, input.Attachments);
                _loadFilterOptions();

                int rootId = _database.GetCurrentGuaranteeByNo(input.GuaranteeNo)?.RootId
                    ?? _database.GetCurrentGuaranteeByNo(input.GuaranteeNo)?.Id
                    ?? 0;
                return GuaranteeActionResult.Success($"تم إضافة الضمان {input.GuaranteeNo}.", rootId);
            });
        }

        public void EditGuarantee(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تعديل الضمان");
            if (current == null)
            {
                return;
            }

            if (!EditGuaranteeDialog.TryShow(
                    current,
                    _database.GetUniqueValues("Bank"),
                    _database.GetUniqueValues("GuaranteeType"),
                    guaranteeNo => GuaranteeDataAccess.GuaranteeNumbersEqual(guaranteeNo, current.GuaranteeNo) || _database.IsGuaranteeNoUnique(guaranteeNo),
                    out EditGuaranteeInput input))
            {
                return;
            }

            ExecuteAction("تعديل الضمان", () =>
            {
                current.Supplier = input.Supplier;
                current.Beneficiary = input.Beneficiary;
                current.Bank = input.Bank;
                current.GuaranteeNo = input.GuaranteeNo;
                current.Amount = input.Amount;
                current.ExpiryDate = input.ExpiryDate;
                current.GuaranteeType = input.GuaranteeType;
                current.ReferenceType = input.ReferenceType;
                current.ReferenceNumber = input.ReferenceNumber;
                current.Notes = input.Notes;

                _database.UpdateGuaranteeWithAttachments(current, input.NewAttachments, input.RemovedAttachments);
                _loadFilterOptions();
                return GuaranteeActionResult.Success($"تم تحديث الضمان {input.GuaranteeNo} وإنشاء إصدار جديد.", target.RootId);
            });
        }

        public OperationalInquiryResult? RunInquiry(GuaranteeRow target, OperationalInquiryOption option)
        {
            Guarantee? current = GetGuarantee(target, "الاستعلامات التشغيلية");
            if (current == null)
            {
                return null;
            }

            ContextActionAvailability availability = GuaranteeInquiryActionSupport.GetAvailability(option.Id, current);
            if (!availability.IsEnabled)
            {
                MessageBox.Show(
                    availability.DisabledReason ?? "هذا الاستعلام غير متاح لهذا السجل حاليًا.",
                    "الاستعلامات التشغيلية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return null;
            }

            OperationalInquiryResult? result = GuaranteeInquiryActionSupport.Execute(
                option.Id,
                current,
                _inquiry,
                PromptForEmployeeName);

            if (result == null)
            {
                _diagnostics.RecordEvent("guarantee.inquiry", "cancelled", new { target.GuaranteeNo, option.Id });
                return null;
            }

            _diagnostics.RecordEvent("guarantee.inquiry", "completed", new
            {
                target.GuaranteeNo,
                option.Id,
                result.CanOpenRequestLetter,
                result.CanOpenResponseDocument
            });
            OperationalInquiryDialog.ShowFor(result, _database, _workflow, _excel);
            return result;
        }

        public void CreateExtensionRequest(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "طلب تمديد");
            if (current == null)
            {
                return;
            }

            DateTime suggestedDate = ExtensionRequestFlowSupport.GetSuggestedRequestedExpiryDate(current);
            if (!PromptDialog.TryShow(
                    "طلب تمديد",
                    "تاريخ الانتهاء المطلوب",
                    suggestedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    out string requestedDateText))
            {
                return;
            }

            if (!DateTime.TryParse(requestedDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime requestedDate))
            {
                MessageBox.Show("صيغة التاريخ غير صحيحة. استخدم مثلاً 2026/12/31.", "طلب تمديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ExtensionRequestFlowSupport.TryValidate(current, requestedDate, Environment.UserName, out string validationReason))
            {
                MessageBox.Show(validationReason, "طلب تمديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string requestNotes = $"طلب تمديد من واجهة الضمانات. {ExtensionRequestFlowSupport.BuildReasonSummary(current)}";
            ExecuteAction("طلب تمديد", () =>
            {
                WorkflowRequest request = _workflow.CreateExtensionRequest(current.Id, requestedDate, requestNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تمديد للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void CreateReleaseRequest(GuaranteeRow target)
        {
            MessageBoxResult result = MessageBox.Show(
                $"تأكيد إنشاء طلب إفراج للضمان {target.GuaranteeNo}؟",
                "طلب إفراج",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            ExecuteAction("طلب إفراج", () =>
            {
                WorkflowRequest request = _workflow.CreateReleaseRequest(target.Id, "طلب إفراج من واجهة الضمانات", Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب إفراج للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void CreateReductionRequest(GuaranteeRow target)
        {
            if (!PromptDialog.TryShow(
                    "طلب تخفيض",
                    "المبلغ المطلوب بعد التخفيض",
                    target.AmountValue.ToString("N0", CultureInfo.InvariantCulture),
                    out string requestedAmountText))
            {
                return;
            }

            string normalizedAmount = requestedAmountText.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            if (!decimal.TryParse(normalizedAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal requestedAmount))
            {
                MessageBox.Show("صيغة المبلغ غير صحيحة.", "طلب تخفيض", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExecuteAction("طلب تخفيض", () =>
            {
                WorkflowRequest request = _workflow.CreateReductionRequest(target.Id, requestedAmount, "طلب تخفيض من واجهة الضمانات", Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تخفيض للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void CreateLiquidationRequest(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "طلب تسييل");
            if (current == null)
            {
                return;
            }

            if (!PromptDialog.TryShow(
                    "طلب تسييل",
                    "ملاحظات الطلب",
                    "طلب تسييل من واجهة الضمانات.",
                    out string requestNotes))
            {
                return;
            }

            string normalizedNotes = string.IsNullOrWhiteSpace(requestNotes)
                ? "طلب تسييل من واجهة الضمانات."
                : requestNotes.Trim();

            ExecuteAction("طلب تسييل", () =>
            {
                WorkflowRequest request = _workflow.CreateLiquidationRequest(current.Id, normalizedNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تسييل للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void CreateVerificationRequest(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "طلب تحقق");
            if (current == null)
            {
                return;
            }

            if (!PromptDialog.TryShow(
                    "طلب تحقق",
                    "ملاحظات الطلب",
                    "طلب تحقق من واجهة الضمانات.",
                    out string requestNotes))
            {
                return;
            }

            string normalizedNotes = string.IsNullOrWhiteSpace(requestNotes)
                ? "طلب تحقق من واجهة الضمانات."
                : requestNotes.Trim();

            ExecuteAction("طلب تحقق", () =>
            {
                WorkflowRequest request = _workflow.CreateVerificationRequest(current.Id, normalizedNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تحقق للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void CreateReplacementRequest(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "طلب استبدال");
            if (current == null)
            {
                return;
            }

            if (!ReplacementRequestDialog.TryShow(
                    current,
                    _database.GetUniqueValues("Bank"),
                    _database.GetUniqueValues("GuaranteeType"),
                    _database.IsGuaranteeNoUnique,
                    out ReplacementRequestInput input))
            {
                return;
            }

            ExecuteAction("طلب استبدال", () =>
            {
                WorkflowRequest request = _workflow.CreateReplacementRequest(
                    current.Id,
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
                    Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب استبدال للضمان {target.GuaranteeNo}.", request.RootGuaranteeId, request.Id);
            });
        }

        public void RegisterBankResponse(GuaranteeRow target)
        {
            List<WorkflowRequest> pendingRequests = _database
                .GetWorkflowRequestsByRootId(target.RootId)
                .Where(request => request.Status == RequestStatus.Pending)
                .OrderByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .ToList();

            if (pendingRequests.Count == 0)
            {
                MessageBox.Show("لا يوجد طلب معلق لهذا الضمان.", "تسجيل رد البنك", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BankResponseDialog.TryShow(
                    pendingRequests,
                    out int requestId,
                    out RequestStatus resultStatus,
                    out string notes,
                    out string responseDocumentPath))
            {
                return;
            }

            ExecuteAction("تسجيل رد البنك", () =>
            {
                _workflow.RecordBankResponse(
                    requestId,
                    resultStatus,
                    notes,
                    string.IsNullOrWhiteSpace(responseDocumentPath) ? null : responseDocumentPath);
                return GuaranteeActionResult.Success($"تم تسجيل رد البنك للضمان {target.GuaranteeNo}.", target.RootId);
            });
        }

        public void RegisterBankResponse(WorkflowRequest request, string guaranteeNo)
        {
            if (request.Status != RequestStatus.Pending)
            {
                MessageBox.Show("هذا الطلب ليس معلقًا، لذلك لا يمكن تسجيل رد جديد عليه.", "تسجيل رد البنك", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BankResponseDialog.TryShow(
                    new[] { request },
                    out int requestId,
                    out RequestStatus resultStatus,
                    out string notes,
                    out string responseDocumentPath))
            {
                return;
            }

            ExecuteAction("تسجيل رد البنك", () =>
            {
                _workflow.RecordBankResponse(
                    requestId,
                    resultStatus,
                    notes,
                    string.IsNullOrWhiteSpace(responseDocumentPath) ? null : responseDocumentPath);
                return GuaranteeActionResult.Success($"تم تسجيل رد البنك للطلب المرتبط بالضمان {guaranteeNo}.", request.RootGuaranteeId);
            });
        }

        public void ShowAttachments(GuaranteeRow target, bool showEmptyMessage)
        {
            if (target.Attachments.Count == 0)
            {
                if (showEmptyMessage)
                {
                    MessageBox.Show("لا توجد مرفقات لهذا الضمان.", "المرفقات", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return;
            }

            AttachmentPickerDialog.ShowFor(
                target.Attachments,
                target.IsCurrentVersion ? $"attachments:{target.RootId}:all" : $"attachments:{target.Id}",
                target.IsCurrentVersion
                    ? $"مرفقات {target.GuaranteeNo} - كل الإصدارات"
                    : $"مرفقات {target.GuaranteeNo} - {target.VersionLabel}");
        }

        public void CopyGuaranteeNo(GuaranteeRow target)
        {
            CopyValue(target.GuaranteeNo, "رقم الضمان", target.GuaranteeNo);
        }

        public void CopySupplier(GuaranteeRow target)
        {
            CopyValue(target.Beneficiary, "اسم المستفيد", target.GuaranteeNo);
        }

        public void CopyReferenceType(GuaranteeRow target)
        {
            CopyValue(target.ReferenceFieldLabel, "نوع المرجع", target.GuaranteeNo);
        }

        public void CopyReferenceNumber(GuaranteeRow target)
        {
            CopyValue(target.ReferenceNumber, "رقم المرجع", target.GuaranteeNo);
        }

        public void CopyGuaranteeType(GuaranteeRow target)
        {
            CopyValue(target.GuaranteeType, "نوع الضمان", target.GuaranteeNo);
        }

        public void CopyIssueDate(GuaranteeRow target)
        {
            CopyValue(target.IssueDate, "تاريخ الإصدار", target.GuaranteeNo);
        }

        public void CopyExpiryDate(GuaranteeRow target)
        {
            CopyValue(target.ExpiryDate, "تاريخ الانتهاء", target.GuaranteeNo);
        }

        public void ShowInquiryResult(OperationalInquiryResult result)
        {
            OperationalInquiryDialog.ShowFor(result, _database, _workflow, _excel);
        }

        public void OpenRequestLetter(WorkflowRequest request)
        {
            if (!request.HasLetter)
            {
                MessageBox.Show("لا يوجد خطاب طلب لهذا السجل.", "خطاب الطلب", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TryOpenWorkflowDocument(
                () => _workflow.OpenRequestLetter(request),
                "خطاب الطلب",
                "تعذر فتح خطاب الطلب. الملف غير موجود.");
        }

        public void OpenResponseDocument(WorkflowRequest request)
        {
            if (!request.HasResponseDocument)
            {
                MessageBox.Show("لا يوجد مستند رد بنك لهذا السجل.", "رد البنك", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TryOpenWorkflowDocument(
                () => _workflow.OpenResponseDocument(request),
                "رد البنك",
                "تعذر فتح رد البنك. الملف غير موجود.");
        }

        public void AttachResponseDocument(WorkflowRequest request, string guaranteeNo)
        {
            if (request.HasResponseDocument)
            {
                OpenResponseDocument(request);
                return;
            }

            if (request.Status == RequestStatus.Pending)
            {
                RegisterBankResponse(request, guaranteeNo);
                return;
            }

            WorkflowRequestListItem listItem = BuildWorkflowRequestListItem(request, guaranteeNo);
            if (!AttachResponseDocumentDialog.TryShow(listItem, out string responsePath, out string additionalNotes))
            {
                return;
            }

            try
            {
                _workflow.AttachResponseDocumentToClosedRequest(request.Id, responsePath, additionalNotes);
                _refreshAfterWorkflowChange(request.RootGuaranteeId, request.Id);
                _shellStatus.ShowSuccess("تم إلحاق مستند رد البنك.", $"الضمانات • {listItem.GuaranteeNo}");
            }
            catch (DeferredFilePromotionException ex)
            {
                _refreshAfterWorkflowChange(request.RootGuaranteeId, request.Id);
                MessageBox.Show(ex.UserMessage, "إلحاق رد البنك", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "إلحاق رد البنك", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Guarantee? GetGuarantee(GuaranteeRow target, string title)
        {
            Guarantee? current = _database.GetGuaranteeById(target.Id);
            if (current == null)
            {
                MessageBox.Show("تعذر العثور على الضمان المحدد.", title, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return current;
        }

        private WorkflowRequestListItem BuildWorkflowRequestListItem(WorkflowRequest request, string guaranteeNo)
        {
            Guarantee? current = _database.GetCurrentGuaranteeByRootId(request.RootGuaranteeId);
            Guarantee? baseVersion = request.BaseVersionId > 0
                ? _database.GetGuaranteeById(request.BaseVersionId)
                : null;
            Guarantee? resultVersion = request.ResultVersionId.HasValue
                ? _database.GetGuaranteeById(request.ResultVersionId.Value)
                : null;
            Guarantee? displayVersion = current ?? resultVersion ?? baseVersion;

            return new WorkflowRequestListItem
            {
                Request = request,
                CurrentGuaranteeId = displayVersion?.Id ?? request.BaseVersionId,
                RootGuaranteeId = request.RootGuaranteeId,
                GuaranteeNo = string.IsNullOrWhiteSpace(guaranteeNo)
                    ? displayVersion?.GuaranteeNo ?? "---"
                    : guaranteeNo,
                Supplier = displayVersion?.Supplier ?? string.Empty,
                Bank = displayVersion?.Bank ?? string.Empty,
                ReferenceType = displayVersion?.ReferenceType ?? GuaranteeReferenceType.None,
                ReferenceNumber = displayVersion?.ReferenceNumber ?? string.Empty,
                CurrentAmount = displayVersion?.Amount ?? 0m,
                CurrentExpiryDate = displayVersion?.ExpiryDate ?? DateTime.MinValue,
                CurrentVersionNumber = current?.VersionNumber ?? displayVersion?.VersionNumber ?? 1,
                BaseVersionNumber = baseVersion?.VersionNumber ?? displayVersion?.VersionNumber ?? 1,
                ResultVersionNumber = resultVersion?.VersionNumber,
                LifecycleStatus = displayVersion?.LifecycleStatus ?? GuaranteeLifecycleStatus.Active
            };
        }

        private static Guarantee? ResolveInquiryGuarantee(OperationalInquiryResult result)
        {
            return result.CurrentGuarantee
                   ?? result.SelectedGuarantee
                   ?? result.ResultGuarantee;
        }

        private void ExecuteAction(string title, Func<GuaranteeActionResult> action)
        {
            try
            {
                GuaranteeActionResult result = action();
                if (result.ShouldRefresh)
                {
                    _refreshAfterWorkflowChange(result.RootIdToRestore, result.RequestIdToFocus);
                }

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    _shellStatus.ShowSuccess(result.Message, $"الضمانات • {title}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CopyValue(string? value, string label, string guaranteeNo)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "---")
            {
                MessageBox.Show($"لا توجد قيمة متاحة لنسخ {label}.", $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(value);
                App.CurrentApp.GetRequiredService<IShellStatusService>().ShowInfo(
                    $"تم نسخ {label}.",
                    $"الضمانات • {guaranteeNo}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string? PromptForEmployeeName()
        {
            return PromptDialog.TryShow(
                "استعلام أداء موظف",
                "اسم الموظف",
                Environment.UserName,
                out string employeeName)
                ? employeeName
                : null;
        }

        private static void TryOpenWorkflowDocument(Action openAction, string title, string missingMessage)
        {
            try
            {
                openAction();
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

        private readonly record struct GuaranteeActionResult(bool ShouldRefresh, int RootIdToRestore, int? RequestIdToFocus, string Message)
        {
            public static GuaranteeActionResult Success(string message, int rootIdToRestore, int? requestIdToFocus = null)
                => new(true, rootIdToRestore, requestIdToFocus, message);
        }
    }
}
