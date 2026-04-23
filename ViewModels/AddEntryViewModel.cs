using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.ViewModels
{
    public sealed class AddEntryViewModel : ViewModelBase
    {
        public sealed class SaveResult
        {
            private SaveResult(bool success, Guarantee? savedGuarantee, string? successStatusMessage, string? warningMessage, string? errorMessage, Exception? exception)
            {
                Success = success;
                SavedGuarantee = savedGuarantee;
                SuccessStatusMessage = successStatusMessage;
                WarningMessage = warningMessage;
                ErrorMessage = errorMessage;
                Exception = exception;
            }

            public bool Success { get; }

            public Guarantee? SavedGuarantee { get; }

            public string? SuccessStatusMessage { get; }

            public string? WarningMessage { get; }

            public string? ErrorMessage { get; }

            public Exception? Exception { get; }

            public static SaveResult Saved(Guarantee? savedGuarantee, string successStatusMessage)
            {
                return new SaveResult(true, savedGuarantee, successStatusMessage, null, null, null);
            }

            public static SaveResult SavedWithWarning(Guarantee? savedGuarantee, string successStatusMessage, string warningMessage)
            {
                return new SaveResult(true, savedGuarantee, successStatusMessage, warningMessage, null, null);
            }

            public static SaveResult Warning(string warningMessage)
            {
                return new SaveResult(false, null, null, warningMessage, null, null);
            }

            public static SaveResult Failure(string errorMessage, Exception exception)
            {
                return new SaveResult(false, null, null, null, errorMessage, exception);
            }
        }

        public sealed class ReferenceTypeOption
        {
            public ReferenceTypeOption(GuaranteeReferenceType value, string label)
            {
                Value = value;
                Label = label;
            }

            public GuaranteeReferenceType Value { get; }

            public string Label { get; }
        }

        private readonly IDatabaseService _dbService;
        private readonly Guarantee? _editSource;
        private readonly List<string> _selectedAttachmentPaths = new();
        private bool _isInitializing = true;
        private bool _isSaving;
        private bool _hasUnsavedChanges;
        private string _formTitle = "إدخال ضمان جديد";
        private string _formHint = "أدخل البيانات الأساسية أولًا، ثم أضف المرجع والمرفقات الرسمية إن وجدت.";
        private string _guaranteeNo = string.Empty;
        private string _amountText = string.Empty;
        private string _supplier = string.Empty;
        private string _bank = string.Empty;
        private string _guaranteeType = string.Empty;
        private string _beneficiary = string.Empty;
        private GuaranteeReferenceType _selectedReferenceType = GuaranteeReferenceType.Contract;
        private DateTime? _expiryDate = DateTime.Today.AddMonths(3);
        private string _referenceNumber = string.Empty;
        private string _notes = string.Empty;
        private string _attachmentSummary = "لم يتم اختيار أي ملفات بعد.";
        private IReadOnlyList<string> _selectedAttachmentNames = Array.Empty<string>();
        private string _guaranteeNoHint = string.Empty;
        private string _amountHint = string.Empty;
        private string _supplierHint = string.Empty;
        private string _bankHint = string.Empty;

        public AddEntryViewModel(IDatabaseService dbService, Guarantee? editSource)
        {
            _dbService = dbService;
            _editSource = editSource;
            ReferenceTypeOptions = new[]
            {
                new ReferenceTypeOption(GuaranteeReferenceType.Contract, "عقد"),
                new ReferenceTypeOption(GuaranteeReferenceType.PurchaseOrder, "أمر شراء")
            };

            if (editSource != null)
            {
                LoadEditSource(editSource);
            }

            RefreshAttachmentSummary();
            _isInitializing = false;
            MarkSaved();
        }

        public IReadOnlyList<ReferenceTypeOption> ReferenceTypeOptions { get; }

        public string FormTitle
        {
            get => _formTitle;
            private set => SetProperty(ref _formTitle, value);
        }

        public string FormHint
        {
            get => _formHint;
            private set => SetProperty(ref _formHint, value);
        }

        public string GuaranteeNo
        {
            get => _guaranteeNo;
            set => SetFormProperty(ref _guaranteeNo, value);
        }

        public string GuaranteeNoHint
        {
            get => _guaranteeNoHint;
            private set
            {
                if (SetProperty(ref _guaranteeNoHint, value))
                {
                    OnPropertyChanged(nameof(HasGuaranteeNoHint));
                }
            }
        }

        public bool HasGuaranteeNoHint => !string.IsNullOrWhiteSpace(GuaranteeNoHint);

        public string AmountText
        {
            get => _amountText;
            set => SetFormProperty(ref _amountText, value);
        }

        public string AmountHint
        {
            get => _amountHint;
            private set
            {
                if (SetProperty(ref _amountHint, value))
                {
                    OnPropertyChanged(nameof(HasAmountHint));
                }
            }
        }

        public bool HasAmountHint => !string.IsNullOrWhiteSpace(AmountHint);

        public string Supplier
        {
            get => _supplier;
            set => SetFormProperty(ref _supplier, value);
        }

        public string SupplierHint
        {
            get => _supplierHint;
            private set
            {
                if (SetProperty(ref _supplierHint, value))
                {
                    OnPropertyChanged(nameof(HasSupplierHint));
                }
            }
        }

        public bool HasSupplierHint => !string.IsNullOrWhiteSpace(SupplierHint);

        public string Bank
        {
            get => _bank;
            set => SetFormProperty(ref _bank, value);
        }

        public string BankHint
        {
            get => _bankHint;
            private set
            {
                if (SetProperty(ref _bankHint, value))
                {
                    OnPropertyChanged(nameof(HasBankHint));
                }
            }
        }

        public bool HasBankHint => !string.IsNullOrWhiteSpace(BankHint);

        public string GuaranteeType
        {
            get => _guaranteeType;
            set => SetFormProperty(ref _guaranteeType, value);
        }

        public string Beneficiary
        {
            get => _beneficiary;
            set => SetFormProperty(ref _beneficiary, value);
        }

        public GuaranteeReferenceType SelectedReferenceType
        {
            get => _selectedReferenceType;
            set => SetFormProperty(ref _selectedReferenceType, value);
        }

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetFormProperty(ref _expiryDate, value);
        }

        public string ReferenceNumber
        {
            get => _referenceNumber;
            set => SetFormProperty(ref _referenceNumber, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetFormProperty(ref _notes, value);
        }

        public string AttachmentSummary
        {
            get => _attachmentSummary;
            private set => SetProperty(ref _attachmentSummary, value);
        }

        public IReadOnlyList<string> SelectedAttachmentNames
        {
            get => _selectedAttachmentNames;
            private set => SetProperty(ref _selectedAttachmentNames, value);
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set => SetProperty(ref _hasUnsavedChanges, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public IReadOnlyList<string> SelectedAttachmentPaths => _selectedAttachmentPaths;

        public void AddAttachments(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths.Where(path => !_selectedAttachmentPaths.Contains(path, StringComparer.OrdinalIgnoreCase)))
            {
                _selectedAttachmentPaths.Add(filePath);
            }

            RefreshAttachmentSummary();
            MarkDirty();
        }

        public bool RemoveAttachmentByFileName(string fileName)
        {
            string? fullPath = _selectedAttachmentPaths.FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));

            if (fullPath == null)
            {
                return false;
            }

            _selectedAttachmentPaths.Remove(fullPath);
            RefreshAttachmentSummary();
            MarkDirty();
            return true;
        }

        public void ClearNewAttachments()
        {
            _selectedAttachmentPaths.Clear();
            RefreshAttachmentSummary();
        }

        public void MarkSaved()
        {
            HasUnsavedChanges = false;
        }

        public void ValidateGuaranteeNo()
        {
            string value = GuaranteeNo.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                GuaranteeNoHint = "رقم الضمان مطلوب";
                return;
            }

            bool isEditWithoutChange = _editSource != null &&
                                       GuaranteeDataAccess.GuaranteeNumbersEqual(_editSource.GuaranteeNo, value);
            if (!isEditWithoutChange && !_dbService.IsGuaranteeNoUnique(value))
            {
                GuaranteeNoHint = "رقم الضمان مستخدم بالفعل";
                return;
            }

            GuaranteeNoHint = string.Empty;
        }

        public void ValidateAmount()
        {
            string value = AmountText.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                AmountHint = "المبلغ مطلوب";
                return;
            }

            if (!decimal.TryParse(value, out decimal parsed) || parsed <= 0)
            {
                AmountHint = "يجب أن يكون المبلغ رقماً موجباً";
                return;
            }

            AmountHint = string.Empty;
        }

        public void ValidateSupplier()
        {
            SupplierHint = string.IsNullOrWhiteSpace(Supplier) ? "حقل المورد مطلوب" : string.Empty;
        }

        public void ValidateBank()
        {
            BankHint = string.IsNullOrWhiteSpace(Bank) ? "حقل البنك مطلوب" : string.Empty;
        }

        public SaveResult Save()
        {
            if (IsSaving)
            {
                return SaveResult.Warning("جاري تنفيذ الحفظ الآن.");
            }

            if (!ValidateForm(out decimal amount, out DateTime expiryDate, out string? warningMessage))
            {
                return SaveResult.Warning(warningMessage ?? "تعذر حفظ بيانات الضمان.");
            }

            try
            {
                IsSaving = true;

                Guarantee guarantee = CreateWorkingGuarantee(amount, expiryDate);
                string successMessage = _editSource == null
                    ? "تم حفظ الضمان الجديد بنجاح."
                    : "تم تحديث بيانات الضمان بنجاح.";

                if (_editSource == null)
                {
                    _dbService.SaveGuarantee(guarantee, _selectedAttachmentPaths.ToList());
                }
                else
                {
                    int updatedId = _dbService.UpdateGuarantee(
                        guarantee,
                        _selectedAttachmentPaths.ToList(),
                        new List<AttachmentRecord>());
                    guarantee.Id = updatedId;
                }

                Guarantee? savedGuarantee = _editSource == null
                    ? _dbService.GetCurrentGuaranteeByNo(guarantee.GuaranteeNo)
                    : _dbService.GetGuaranteeById(guarantee.Id) ?? _dbService.GetCurrentGuaranteeByNo(guarantee.GuaranteeNo);

                ClearNewAttachments();
                ClearHints();
                MarkSaved();

                return SaveResult.Saved(savedGuarantee, successMessage);
            }
            catch (DeferredFilePromotionException ex)
            {
                Guarantee? savedGuarantee = _editSource == null
                    ? _dbService.GetCurrentGuaranteeByNo(GuaranteeNo.Trim())
                    : _dbService.GetCurrentGuaranteeByNo(GuaranteeNo.Trim()) ?? _dbService.GetGuaranteeById(_editSource.Id);

                ClearNewAttachments();
                ClearHints();
                MarkSaved();

                string successMessage = _editSource == null
                    ? "تم حفظ الضمان الجديد، لكن بعض المرفقات ما زالت بانتظار الاستكمال."
                    : "تم تحديث بيانات الضمان، لكن بعض المرفقات ما زالت بانتظار الاستكمال.";

                return SaveResult.SavedWithWarning(savedGuarantee, successMessage, ex.UserMessage);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure("تعذر حفظ بيانات الضمان.", ex);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void LoadEditSource(Guarantee editSource)
        {
            FormTitle = $"تعديل الضمان {editSource.GuaranteeNo}";
            FormHint = "تحديث الإصدار الحالي من نفس مساحة الإدخال الموحدة.";
            _guaranteeNo = editSource.GuaranteeNo;
            _amountText = editSource.Amount.ToString("0.##");
            _supplier = editSource.Supplier;
            _bank = editSource.Bank;
            _guaranteeType = editSource.GuaranteeType;
            _beneficiary = editSource.Beneficiary;
            _expiryDate = editSource.ExpiryDate;
            _selectedReferenceType = editSource.ReferenceType == GuaranteeReferenceType.PurchaseOrder
                ? GuaranteeReferenceType.PurchaseOrder
                : GuaranteeReferenceType.Contract;
            _referenceNumber = editSource.ReferenceNumber;
            _notes = editSource.Notes;
        }

        private void RefreshAttachmentSummary()
        {
            SelectedAttachmentNames = _selectedAttachmentPaths
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToList();
            AttachmentSummary = _selectedAttachmentPaths.Count == 0
                ? "لم يتم اختيار أي ملفات بعد."
                : $"عدد الملفات الجديدة المختارة: {_selectedAttachmentPaths.Count}.";
        }

        private bool SetFormProperty<T>(ref T storage, T value)
        {
            bool changed = SetProperty(ref storage, value);
            if (changed)
            {
                MarkDirty();
            }

            return changed;
        }

        private void MarkDirty()
        {
            if (_isInitializing || IsSaving)
            {
                return;
            }

            HasUnsavedChanges = true;
        }

        private void ClearHints()
        {
            GuaranteeNoHint = string.Empty;
            AmountHint = string.Empty;
            SupplierHint = string.Empty;
            BankHint = string.Empty;
        }

        private Guarantee CreateWorkingGuarantee(decimal amount, DateTime expiryDate)
        {
            Guarantee guarantee = _editSource == null
                ? new Guarantee()
                : new Guarantee
                {
                    Id = _editSource.Id,
                    RootId = _editSource.RootId,
                    VersionNumber = _editSource.VersionNumber,
                    CreatedAt = _editSource.CreatedAt,
                    IsCurrent = _editSource.IsCurrent,
                    LifecycleStatus = _editSource.LifecycleStatus,
                    ReplacedByRootId = _editSource.ReplacedByRootId,
                    ReplacesRootId = _editSource.ReplacesRootId,
                    Attachments = _editSource.Attachments
                };

            guarantee.GuaranteeNo = GuaranteeNo.Trim();
            guarantee.Amount = amount;
            guarantee.Supplier = Supplier.Trim();
            guarantee.Bank = Bank.Trim();
            guarantee.GuaranteeType = GuaranteeType.Trim();
            guarantee.Beneficiary = Beneficiary.Trim();
            guarantee.ExpiryDate = expiryDate.Date;
            guarantee.ReferenceType = SelectedReferenceType;
            guarantee.ReferenceNumber = ReferenceNumber.Trim();
            guarantee.Notes = Notes.Trim();

            return guarantee;
        }

        private bool ValidateForm(out decimal amount, out DateTime expiryDate, out string? warningMessage)
        {
            amount = 0;
            expiryDate = DateTime.Today;
            warningMessage = null;

            ValidateGuaranteeNo();
            ValidateAmount();
            ValidateSupplier();
            ValidateBank();

            if (HasGuaranteeNoHint)
            {
                warningMessage = GuaranteeNoHint;
                return false;
            }

            if (HasAmountHint)
            {
                warningMessage = AmountHint;
                return false;
            }

            if (HasSupplierHint)
            {
                warningMessage = SupplierHint;
                return false;
            }

            if (HasBankHint)
            {
                warningMessage = BankHint;
                return false;
            }

            if (string.IsNullOrWhiteSpace(GuaranteeType) || string.IsNullOrWhiteSpace(Beneficiary))
            {
                warningMessage = "أكمل الحقول الأساسية: المورد، البنك، النوع، والجهة المستفيدة.";
                return false;
            }

            if (!decimal.TryParse(AmountText.Trim(), out amount) || amount <= 0)
            {
                AmountHint = "يجب أن يكون المبلغ رقماً موجباً";
                warningMessage = AmountHint;
                return false;
            }

            if (!ExpiryDate.HasValue)
            {
                warningMessage = "حدد تاريخ الانتهاء.";
                return false;
            }

            expiryDate = ExpiryDate.Value;
            return true;
        }
    }
}
