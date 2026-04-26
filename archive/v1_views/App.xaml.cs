using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using GuaranteeManager.Contracts;
using GuaranteeManager.Services;
using GuaranteeManager.UI_V2.Shell;
using GuaranteeManager.Utils;
using GuaranteeManager.ViewModels;
#if DEBUG
using GuaranteeManager.Development;
#endif
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

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
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            // 1. Setup Navigation & Directories
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
                MessageBox.Show(message, "خطأ في بدء التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            // 4. Trigger Automatic Backup
            ConfigureServices();

            try
            {
                Services.GetRequiredService<BackupService>().PerformAutoBackup();
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "App Startup (Backup)");
            }

            MainWindow = new UI_V2.Shell.V2ShellWindow();
            MainWindow.Show();
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
                MessageBox.Show(
                    message,
                    "تنبيه في النظام",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                e.Handled = true;
                return;
            }

            e.Handled = true;
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
                MessageBox.Show(
                    message,
                    "توقف غير متوقع",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            services.AddSingleton<IOperationalInquiryService>(provider =>
                new OperationalInquiryService(provider.GetRequiredService<IDatabaseService>()));
            services.AddSingleton<IContextActionService, ContextActionService>();
            services.AddSingleton<IViewFactory, ViewFactory>();
            services.AddSingleton<ShellViewModel>();
#if DEBUG
            services.AddSingleton<DataSeedingService>();
#endif
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();
        }

        private static bool IsUiV2PreviewRequested(string[] args)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--ui-v2-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
