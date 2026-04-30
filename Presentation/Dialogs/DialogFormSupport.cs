using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    internal static class DialogFormSupport
    {
        public static void WireDirtyTracking(Action markDirty, params FrameworkElement[] elements)
        {
            foreach (FrameworkElement element in elements)
            {
                switch (element)
                {
                    case TextBox textBox:
                        textBox.TextChanged += (_, _) => markDirty();
                        break;
                    case ComboBox comboBox:
                        comboBox.SelectionChanged += (_, _) => markDirty();
                        if (comboBox.IsEditable)
                        {
                            comboBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => markDirty()));
                        }
                        break;
                }
            }
        }

        public static bool ConfirmDiscardChanges()
        {
            return App.CurrentApp.GetRequiredService<IAppDialogService>().Confirm(
                "لديك تعديلات غير محفوظة. هل تريد إغلاق النافذة وفقدان هذه التعديلات؟",
                "تأكيد الإغلاق");
        }

        public static void RunWorkspaceReport(string ownerTitle)
        {
            if (!ReportPickerDialog.TryShow(out string reportKey))
            {
                return;
            }

            string? input = null;
            if (WorkspaceReportCatalog.RequiresInput(reportKey)
                && !GuidedTextPromptDialog.TryShow(
                    ownerTitle,
                    WorkspaceReportCatalog.GetInputPrompt(reportKey),
                    WorkspaceReportCatalog.GetInputLabel(reportKey),
                    "إنشاء التقرير",
                    string.Empty,
                    out input))
            {
                return;
            }

            IDatabaseService database = App.CurrentApp.GetRequiredService<IDatabaseService>();
            IExcelService excel = App.CurrentApp.GetRequiredService<IExcelService>();
            IGuaranteeHistoryDocumentService historyDocuments = App.CurrentApp.GetRequiredService<IGuaranteeHistoryDocumentService>();

            bool exported = WorkspaceReportCatalog.Run(reportKey, database, excel, input, historyDocuments);
            string reportTitle = WorkspaceReportCatalog.PortfolioActions
                .Concat(WorkspaceReportCatalog.OperationalActions)
                .FirstOrDefault(action => action.Key == reportKey)?.Title ?? "التقرير";

            IAppDialogService dialogs = App.CurrentApp.GetRequiredService<IAppDialogService>();
            if (exported)
            {
                string successMessage = WorkspaceReportCatalog.IsPrintAction(reportKey)
                    ? $"تم إرسال {reportTitle} إلى الطباعة."
                    : $"تم إنشاء تقرير {reportTitle} من البيانات المحفوظة الحالية.";
                dialogs.ShowInformation(
                    successMessage,
                    ownerTitle);
            }
            else
            {
                dialogs.ShowWarning(
                    $"تم إلغاء إنشاء تقرير {reportTitle}.",
                    ownerTitle);
            }
        }
    }
}
