using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
#if DEBUG
using GuaranteeManager.Development;
using GuaranteeManager.Services.Seeding;
#endif
using Microsoft.Extensions.DependencyInjection;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public partial class App : Application
    {
        private static System.Threading.Mutex? _mutex;
        private static bool _ownsMutex;

        public IServiceProvider Services { get; private set; } = null!;

        public static App CurrentApp => (App)Current;

        public T GetRequiredService<T>() where T : notnull
        {
            return Services.GetRequiredService<T>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "BankGuaranteeManager_Mutex";
            bool createdNew;

            _mutex = new System.Threading.Mutex(true, appName, out createdNew);
            _ownsMutex = createdNew;

            if (!createdNew)
            {
                SimpleLogger.Log("Startup blocked because another application instance is already running.", "WARNING");
                MessageBox.Show("التطبيق يعمل بالفعل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 1. Setup directories
            AppPaths.EnsureDirectoriesExist();
            SimpleLogger.Log($"Application session started. StorageRoot={AppPaths.StorageRootDirectory}");

            // 2. Setup Global Error Handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 3. Initialize database runtime once before any window uses it.
            try
            {
                DatabaseService.InitializeRuntime();
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "App Startup (Database Init)");
                string message = ex is ApplicationOperationException operationException
                    ? operationException.UserMessageWithReference
                    : "تعذر تهيئة قاعدة بيانات النظام. تم تسجيل الخطأ في المجلد Logs.";
                RecordRuntimeFault(
                    action: "startup.database-init-failed",
                    severity: "Error",
                    title: "خطأ في بدء التشغيل",
                    message: message,
                    exception: ex,
                    isTerminating: true);
                MessageBox.Show(message, "خطأ في بدء التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            // 4. Trigger Automatic Backup
            ConfigureServices();

            StartAutoBackupInBackground();

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = new MainWindow();
            MainWindow.Show();

#if DEBUG
            if (e.Args.Any(arg => string.Equals(arg, "--dialog-audit", StringComparison.OrdinalIgnoreCase)))
            {
                bool exitAfterAudit = e.Args.Any(arg => string.Equals(arg, "--exit-after-dialog-audit", StringComparison.OrdinalIgnoreCase));
                DialogAuditRunner.Start(MainWindow, exitAfterAudit);
            }
#endif
        }

        private void StartAutoBackupInBackground()
        {
            BackupService backupService = Services.GetRequiredService<BackupService>();
            _ = Task.Run(() => backupService.PerformAutoBackup())
                .ContinueWith(task =>
                {
                    Exception? ex = task.Exception?.GetBaseException();
                    if (ex == null)
                    {
                        return;
                    }

                    SimpleLogger.LogError(ex, "App Startup (Backup)");
                    RecordRuntimeFault(
                        action: "startup.backup-failed",
                        severity: "Warning",
                        title: "خطأ في النسخة الاحتياطية التلقائية",
                        message: ex.Message,
                        exception: ex,
                        isTerminating: false);
                }, TaskScheduler.Default);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SimpleLogger.Log($"Application session ended with exit code {e.ApplicationExitCode}.");

            try
            {
                if (_ownsMutex)
                {
                    _mutex?.ReleaseMutex();
                }
            }
            catch (ApplicationException ex)
            {
                SimpleLogger.LogError(ex, "App Exit (ReleaseMutex)");
            }
            finally
            {
                _mutex?.Dispose();
                _mutex = null;
                _ownsMutex = false;
            }

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            SimpleLogger.LogError(e.Exception, "Unhandled UI Exception");

            if (IsRecoverableUnhandledException(e.Exception))
            {
                string message = e.Exception is ApplicationOperationException operationException
                    ? operationException.UserMessageWithReference
                    : "حدث خطأ أثناء تنفيذ العملية الحالية. تم إلغاء العملية مع بقاء التطبيق مفتوحًا، وتم تسجيل التفاصيل في المجلد Logs.";
                RecordRuntimeFault(
                    action: "dispatcher-unhandled-recoverable",
                    severity: "Warning",
                    title: "تنبيه في النظام",
                    message: message,
                    exception: e.Exception,
                    isTerminating: false);
                MessageBox.Show(
                    message,
                    "تنبيه في النظام",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                e.Handled = true;
                return;
            }

            e.Handled = true;
            RecordRuntimeFault(
                action: "dispatcher-unhandled-critical",
                severity: "Error",
                title: "خطأ جسيم في النظام",
                message: "حدث خطأ تقني غير متوقع وقد يترك النظام في حالة غير مستقرة. سيتم إغلاق البرنامج بشكل آمن بعد تسجيل الخطأ في المجلد Logs.",
                exception: e.Exception,
                isTerminating: true);
            ShowCriticalFailureAndShutdown(
                "حدث خطأ تقني غير متوقع وقد يترك النظام في حالة غير مستقرة. سيتم إغلاق البرنامج بشكل آمن بعد تسجيل الخطأ في المجلد Logs.",
                "خطأ جسيم في النظام");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? capturedException = e.ExceptionObject as Exception;

            if (capturedException != null)
            {
                SimpleLogger.LogError(capturedException, "AppDomain Unhandled Exception");
            }

            if (e.IsTerminating)
            {
                string message = capturedException is ApplicationOperationException operationException
                    ? operationException.UserMessageWithReference
                    : "حدث خطأ جسيم أدى إلى إيقاف التطبيق. تم تسجيل التفاصيل في المجلد Logs.";
                RecordRuntimeFault(
                    action: "appdomain-unhandled-terminating",
                    severity: "Error",
                    title: "توقف غير متوقع",
                    message: message,
                    exception: capturedException,
                    isTerminating: true);
                MessageBox.Show(
                    message,
                    "توقف غير متوقع",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void RecordRuntimeFault(
            string action,
            string severity,
            string title,
            string message,
            Exception? exception,
            bool isTerminating)
        {
            try
            {
                new UiDiagnosticsService().RecordEvent(
                    "runtime.fault",
                    action,
                    new
                    {
                        Severity = severity,
                        Title = title,
                        Message = message,
                        IsTerminating = isTerminating,
                        ExceptionType = exception?.GetType().FullName,
                        ExceptionMessage = exception?.Message
                    });
            }
            catch
            {
            }
        }

        private static bool IsRecoverableUnhandledException(Exception ex)
        {
            return ex is OperationCanceledException
                or TaskCanceledException
                or FileNotFoundException
                or DirectoryNotFoundException
                or ApplicationOperationException { IsCritical: false };
        }

        private void ShowCriticalFailureAndShutdown(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

            try
            {
                Shutdown();
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        private void ConfigureServices()
        {
            ServiceCollection services = new();

            services.AddSingleton<IUiDiagnosticsService, UiDiagnosticsService>();
            services.AddSingleton<IShellStatusService, ShellStatusService>();
            services.AddSingleton<IAppDialogService, AppDialogService>();
            services.AddSingleton<INavigationGuard, NavigationGuardService>();
            services.AddSingleton<SecondaryWindowManager>();
            services.AddSingleton<AttachmentStorageService>();
            services.AddSingleton<WorkflowResponseStorageService>();
            services.AddSingleton<WorkflowLetterService>();
            services.AddSingleton<BackupService>(_ => new BackupService($"Data Source={AppPaths.DatabasePath}"));
            services.AddSingleton<IDatabaseService>(provider =>
                new DatabaseService(provider.GetRequiredService<AttachmentStorageService>()));
            services.AddSingleton<IWorkflowService>(provider =>
                new WorkflowService(
                    provider.GetRequiredService<IDatabaseService>(),
                    provider.GetRequiredService<WorkflowLetterService>(),
                    provider.GetRequiredService<WorkflowResponseStorageService>()));
            services.AddSingleton<IExcelService, ExcelService>();
            services.AddSingleton<IGuaranteeHistoryDocumentService, GuaranteeHistoryDocumentService>();
#if DEBUG
            services.AddSingleton<DataSeedingService>();
#endif

            Services = services.BuildServiceProvider();
        }
    }
}
