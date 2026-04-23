using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.ViewModels
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly BackupService _backupService;

        private string _guaranteeCount = "0";
        private string _requestCount = "0";
        private string _attachmentCount = "0";
        private string _storageRootPath = "--";
        private string _baseDirectoryPath = "--";
        private string _databasePath = "--";
        private string _attachmentsPath = "--";
        private string _workflowPath = "--";
        private string _logsPath = "--";
        private string _lastBackupPath = "لم يتم إنشاء نسخة احتياطية يدوية في هذه الجلسة.";
        private string _lastRestorePath = "لم يتم تنفيذ استرجاع في هذه الجلسة.";
        private string _lastPortablePackagePath = "لم يتم إنشاء حزمة محمولة في هذه الجلسة.";
        private string _lastPortableRestorePath = "لم يتم استرجاع حزمة محمولة في هذه الجلسة.";

        public SettingsViewModel(
            IDatabaseService databaseService,
            BackupService backupService)
        {
            _databaseService = databaseService;
            _backupService = backupService;
        }

        public string GuaranteeCount
        {
            get => _guaranteeCount;
            private set => SetProperty(ref _guaranteeCount, value);
        }

        public string RequestCount
        {
            get => _requestCount;
            private set => SetProperty(ref _requestCount, value);
        }

        public string AttachmentCount
        {
            get => _attachmentCount;
            private set => SetProperty(ref _attachmentCount, value);
        }

        public string DatabasePath
        {
            get => _databasePath;
            private set => SetProperty(ref _databasePath, value);
        }

        public string StorageRootPath
        {
            get => _storageRootPath;
            private set => SetProperty(ref _storageRootPath, value);
        }

        public string BaseDirectoryPath
        {
            get => _baseDirectoryPath;
            private set => SetProperty(ref _baseDirectoryPath, value);
        }

        public string AttachmentsPath
        {
            get => _attachmentsPath;
            private set => SetProperty(ref _attachmentsPath, value);
        }

        public string WorkflowPath
        {
            get => _workflowPath;
            private set => SetProperty(ref _workflowPath, value);
        }

        public string LogsPath
        {
            get => _logsPath;
            private set => SetProperty(ref _logsPath, value);
        }

        public string LastBackupPath
        {
            get => _lastBackupPath;
            private set => SetProperty(ref _lastBackupPath, value);
        }

        public string LastRestorePath
        {
            get => _lastRestorePath;
            private set => SetProperty(ref _lastRestorePath, value);
        }

        public string LastPortablePackagePath
        {
            get => _lastPortablePackagePath;
            private set => SetProperty(ref _lastPortablePackagePath, value);
        }

        public string LastPortableRestorePath
        {
            get => _lastPortableRestorePath;
            private set => SetProperty(ref _lastPortableRestorePath, value);
        }

        public void Refresh()
        {
            GuaranteeCount = _databaseService.CountGuarantees().ToString();
            RequestCount = _databaseService.CountWorkflowRequests().ToString();
            AttachmentCount = _databaseService.CountAttachments().ToString();
            StorageRootPath = AppPaths.StorageRootDirectory;
            BaseDirectoryPath = AppPaths.BaseDirectory;
            DatabasePath = AppPaths.DatabasePath;
            AttachmentsPath = AppPaths.AttachmentsFolder;
            WorkflowPath = AppPaths.WorkflowFolder;
            LogsPath = AppPaths.LogsFolder;
            LastBackupPath = string.IsNullOrWhiteSpace(_backupService.LastManualBackupPath)
                ? "لم يتم إنشاء نسخة احتياطية يدوية في هذه الجلسة."
                : _backupService.LastManualBackupPath;
            LastRestorePath = string.IsNullOrWhiteSpace(_backupService.LastRestoreSourcePath)
                ? "لم يتم تنفيذ استرجاع في هذه الجلسة."
                : BuildRestoreStatusMessage();
            LastPortablePackagePath = string.IsNullOrWhiteSpace(_backupService.LastPortableBackupPackagePath)
                ? "لم يتم إنشاء حزمة محمولة في هذه الجلسة."
                : _backupService.LastPortableBackupPackagePath;
            LastPortableRestorePath = string.IsNullOrWhiteSpace(_backupService.LastPortableRestorePackagePath)
                ? "لم يتم استرجاع حزمة محمولة في هذه الجلسة."
                : BuildPortableRestoreStatusMessage();
        }

        private string BuildRestoreStatusMessage()
        {
            if (string.IsNullOrWhiteSpace(_backupService.LastPreRestoreSafetyBackupPath))
            {
                return _backupService.LastRestoreSourcePath ?? "لم يتم تنفيذ استرجاع في هذه الجلسة.";
            }

            return $"{_backupService.LastRestoreSourcePath}{Environment.NewLine}نسخة أمان قبل الاسترجاع: {_backupService.LastPreRestoreSafetyBackupPath}";
        }

        private string BuildPortableRestoreStatusMessage()
        {
            if (string.IsNullOrWhiteSpace(_backupService.LastPortableRestoreSafetyPackagePath))
            {
                return _backupService.LastPortableRestorePackagePath ?? "لم يتم استرجاع حزمة محمولة في هذه الجلسة.";
            }

            return $"{_backupService.LastPortableRestorePackagePath}{Environment.NewLine}حزمة أمان قبل الاسترجاع: {_backupService.LastPortableRestoreSafetyPackagePath}";
        }
    }
}
