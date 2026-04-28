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
        private readonly IGuaranteeHistoryDocumentService _historyDocuments;
        private readonly IOperationalInquiryService _inquiry;
        private readonly IShellStatusService _shellStatus;
        private readonly IUiDiagnosticsService _diagnostics;
        private readonly Action _loadFilterOptions;
        private readonly Action<int> _refreshAfterWorkflowChange;

        public GuaranteeWorkspaceCoordinator(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            IGuaranteeHistoryDocumentService historyDocuments,
            IOperationalInquiryService inquiry,
            IShellStatusService shellStatus,
            Action loadFilterOptions,
            Action<int> refreshAfterWorkflowChange)
        {
            _database = database;
            _workflow = workflow;
            _excel = excel;
            _historyDocuments = historyDocuments;
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

            ExecuteAction("إجراء جديد", () =>
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

                _database.SaveGuarantee(guarantee, input.AttachmentPaths);
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

                _database.UpdateGuarantee(current, input.NewAttachmentPaths, input.RemovedAttachments);
                _loadFilterOptions();
                return GuaranteeActionResult.Success($"تم تحديث الضمان {input.GuaranteeNo} وإنشاء إصدار جديد.", target.RootId);
            });
        }

        public void OpenHistory(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "سجل الضمان");
            if (current == null)
            {
                return;
            }

            List<Guarantee> history = _database.GetGuaranteeHistory(current.Id);
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(target.RootId);
            _diagnostics.RecordEvent("guarantee.history", "open", new { target.GuaranteeNo, target.RootId, HistoryCount = history.Count, RequestCount = requests.Count });
            HistoryDialog.ShowFor(target, history, requests);
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
                result.CanOpenHistory,
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
                _workflow.CreateExtensionRequest(current.Id, requestedDate, requestNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تمديد للضمان {target.GuaranteeNo}.", target.RootId);
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
                _workflow.CreateReleaseRequest(target.Id, "طلب إفراج من واجهة الضمانات", Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب إفراج للضمان {target.GuaranteeNo}.", target.RootId);
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
                _workflow.CreateReductionRequest(target.Id, requestedAmount, "طلب تخفيض من واجهة الضمانات", Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تخفيض للضمان {target.GuaranteeNo}.", target.RootId);
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
                _workflow.CreateLiquidationRequest(current.Id, normalizedNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تسييل للضمان {target.GuaranteeNo}.", target.RootId);
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
                _workflow.CreateVerificationRequest(current.Id, normalizedNotes, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب تحقق للضمان {target.GuaranteeNo}.", target.RootId);
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
                _workflow.CreateReplacementRequest(
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
                return GuaranteeActionResult.Success($"تم إنشاء طلب استبدال للضمان {target.GuaranteeNo}.", target.RootId);
            });
        }

        public void CreateAnnulmentRequest(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "طلب نقض");
            if (current == null)
            {
                return;
            }

            if (!PromptDialog.TryShow(
                    "طلب نقض",
                    "سبب الطلب",
                    "طلب نقض من واجهة الضمانات.",
                    out string requestReason))
            {
                return;
            }

            string normalizedReason = string.IsNullOrWhiteSpace(requestReason)
                ? "طلب نقض من واجهة الضمانات."
                : requestReason.Trim();

            ExecuteAction("طلب نقض", () =>
            {
                _workflow.CreateAnnulmentRequest(current.Id, normalizedReason, Environment.UserName);
                return GuaranteeActionResult.Success($"تم إنشاء طلب نقض للضمان {target.GuaranteeNo}.", target.RootId);
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

            AttachmentPickerDialog.ShowFor(target.Attachments, $"attachments:{target.RootId}");
        }

        public void ShowRequests(GuaranteeRow target, bool requireExistingRequests, int? initialRequestId = null)
        {
            int rootId = target.RootId;
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            if (requireExistingRequests && requests.Count == 0)
            {
                MessageBox.Show("لا توجد طلبات مرتبطة بهذا الضمان.", "طلبات الضمان", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RequestsDialog.ShowFor(
                () => _database.GetWorkflowRequestsByRootId(rootId),
                _workflow,
                () => _refreshAfterWorkflowChange(rootId),
                $"requests:{rootId}",
                initialRequestId);
        }

        public void CopyGuaranteeNo(GuaranteeRow target)
        {
            CopyValue(target.GuaranteeNo, "رقم الضمان");
        }

        public void CopySupplier(GuaranteeRow target)
        {
            CopyValue(target.Beneficiary, "اسم المستفيد");
        }

        public void CopyReferenceType(GuaranteeRow target)
        {
            CopyValue(target.ReferenceFieldLabel, "نوع المرجع");
        }

        public void CopyReferenceNumber(GuaranteeRow target)
        {
            CopyValue(target.ReferenceNumber, "رقم المرجع");
        }

        public void ExportVisibleGuarantees(IReadOnlyList<GuaranteeRow> rows)
        {
            if (rows.Count == 0)
            {
                MessageBox.Show("لا توجد نتائج معروضة حاليًا لتصديرها.", "تصدير النتائج الحالية", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<Guarantee> guarantees = rows
                .Select(row => _database.GetGuaranteeById(row.Id))
                .Where(guarantee => guarantee != null)
                .Cast<Guarantee>()
                .ToList();

            if (guarantees.Count == 0)
            {
                MessageBox.Show("تعذر تحميل الضمانات المعروضة حاليًا للتصدير.", "تصدير النتائج الحالية", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ExecuteExport(
                "تصدير النتائج الحالية",
                $"تم تصدير {guarantees.Count.ToString("N0", CultureInfo.InvariantCulture)} ضمان من النتائج المعروضة.",
                () => _excel.ExportGuarantees(guarantees),
                () => _diagnostics.RecordEvent("guarantee.export", "visible-results", new { Count = guarantees.Count }));
        }

        public void ExportGuaranteeReport(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تصدير تقرير الضمان");
            if (current == null)
            {
                return;
            }

            ExecuteExport(
                "تصدير تقرير الضمان",
                $"تم تصدير تقرير الضمان {target.GuaranteeNo}.",
                () => _excel.ExportSingleGuaranteeReport(current),
                () => _diagnostics.RecordEvent("guarantee.export", "report", new { target.GuaranteeNo, target.RootId }));
        }

        public void ExportGuaranteeHistory(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تصدير سجل الضمان");
            if (current == null)
            {
                return;
            }

            List<Guarantee> history = _database.GetGuaranteeHistory(current.Id);
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(target.RootId);
            ExecuteExport(
                "تصدير سجل الضمان",
                $"تم تصدير سجل الضمان {target.GuaranteeNo}.",
                () => _historyDocuments.ExportHistoryToExcel(current, history, requests),
                () => _diagnostics.RecordEvent("guarantee.export", "history", new { target.GuaranteeNo, target.RootId, HistoryCount = history.Count, RequestCount = requests.Count }));
        }

        public void ExportGuaranteesByBank(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تصدير ضمانات البنك");
            if (current == null || string.IsNullOrWhiteSpace(current.Bank))
            {
                MessageBox.Show("لا يوجد بنك مرتبط بهذا الضمان.", "تصدير ضمانات البنك", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<Guarantee> guarantees = _database.QueryGuarantees(new GuaranteeQueryOptions
            {
                Bank = current.Bank,
                IncludeAttachments = false,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

            ExecuteExport(
                "تصدير ضمانات البنك",
                $"تم تصدير ضمانات البنك {current.Bank}.",
                () => _excel.ExportGuaranteesByBank(current.Bank, guarantees),
                () => _diagnostics.RecordEvent("guarantee.export", "by-bank", new { current.Bank, Count = guarantees.Count }));
        }

        public void ExportGuaranteesBySupplier(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تصدير ضمانات المورد");
            if (current == null || string.IsNullOrWhiteSpace(current.Supplier))
            {
                MessageBox.Show("لا يوجد مورد مرتبط بهذا الضمان.", "تصدير ضمانات المورد", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<Guarantee> guarantees = _database.QueryGuarantees(new GuaranteeQueryOptions
            {
                Supplier = current.Supplier,
                IncludeAttachments = false,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

            ExecuteExport(
                "تصدير ضمانات المورد",
                $"تم تصدير ضمانات المورد {current.Supplier}.",
                () => _excel.ExportGuaranteesBySupplier(current.Supplier, guarantees),
                () => _diagnostics.RecordEvent("guarantee.export", "by-supplier", new { current.Supplier, Count = guarantees.Count }));
        }

        public void ExportGuaranteesByTemporalStatus(GuaranteeRow target)
        {
            Guarantee? current = GetGuarantee(target, "تصدير حسب الحالة الزمنية");
            if (current == null)
            {
                return;
            }

            GuaranteeTimeStatus timeStatus = ResolveTimeStatus(current);
            List<Guarantee> guarantees = _database.QueryGuarantees(new GuaranteeQueryOptions
            {
                TimeStatus = timeStatus,
                IncludeAttachments = false,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

            ExecuteExport(
                "تصدير حسب الحالة الزمنية",
                $"تم تصدير ضمانات الحالة {current.StatusLabel}.",
                () => _excel.ExportGuaranteesByTemporalStatus(current.StatusLabel, guarantees),
                () => _diagnostics.RecordEvent("guarantee.export", "by-time-status", new { current.StatusLabel, Count = guarantees.Count }));
        }

        public void ShowInquiryResult(OperationalInquiryResult result)
        {
            OperationalInquiryDialog.ShowFor(result, _database, _workflow, _excel);
        }

        public void OpenInquiryHistory(OperationalInquiryResult result)
        {
            Guarantee? guarantee = ResolveInquiryGuarantee(result);
            if (guarantee == null)
            {
                MessageBox.Show("تعذر تحديد الضمان المرجعي لهذا الجواب.", "نتيجة الاستعلام", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            Guarantee currentGuarantee = _database.GetCurrentGuaranteeByRootId(rootId) ?? guarantee;
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            List<Guarantee> history = _database.GetGuaranteeHistory(currentGuarantee.Id);
            GuaranteeRow row = GuaranteeRow.FromGuarantee(currentGuarantee, requests);
            HistoryDialog.ShowFor(
                row,
                history,
                requests,
                result.RelatedRequest?.Id,
                preferRequestsTab: result.RelatedRequest != null);
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

        private Guarantee? GetGuarantee(GuaranteeRow target, string title)
        {
            Guarantee? current = _database.GetGuaranteeById(target.Id);
            if (current == null)
            {
                MessageBox.Show("تعذر العثور على الضمان المحدد.", title, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return current;
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
                    _refreshAfterWorkflowChange(result.RootIdToRestore);
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

        private static GuaranteeTimeStatus ResolveTimeStatus(Guarantee guarantee)
        {
            if (guarantee.IsExpired)
            {
                return GuaranteeTimeStatus.Expired;
            }

            return guarantee.IsExpiringSoon
                ? GuaranteeTimeStatus.ExpiringSoon
                : GuaranteeTimeStatus.Active;
        }

        private static void CopyValue(string? value, string label)
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
                    "الحافظة جاهزة للاستخدام");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"نسخ {label}", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void ExecuteExport(string title, string successMessage, Func<bool> action, Action? onSuccess = null)
        {
            try
            {
                if (!action())
                {
                    return;
                }

                onSuccess?.Invoke();
                App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(successMessage, $"الضمانات • {title}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private readonly record struct GuaranteeActionResult(bool ShouldRefresh, int RootIdToRestore, string Message)
        {
            public static GuaranteeActionResult Success(string message, int rootIdToRestore)
                => new(true, rootIdToRestore, message);
        }
    }
}
