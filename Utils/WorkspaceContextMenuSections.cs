using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Utils
{
    public static class WorkspaceContextMenuSections
    {
        public static IReadOnlyList<ContextActionSection> BuildGuaranteeSections(IContextActionService contextActionService, bool includeVisibleListExport)
        {
            IReadOnlyList<ContextActionSection> sourceSections = contextActionService.GetGuaranteeActions();
            ContextActionSection? inquirySection = sourceSections.FirstOrDefault(section => section.Header == "افهم");

            var openItems = new List<ContextActionDefinition>
            {
                ContextActionDefinition.Action(
                    "workspace.guarantee.open-file",
                    "فتح ملف الضمان",
                    ContextActionResultKind.Navigation,
                    "يفتح ملف الضمان الكامل من نفس الصف المحدد.")
            };
            openItems.AddRange(ContextActionMenuFactory.FindActionsByIds(sourceSections, "navigate.history", "evidence.attachments"));

            var exportActionIds = includeVisibleListExport
                ? new[] { "export.guarantee-report", "export.guarantee-history", "export.same-bank", "export.same-supplier", "export.same-temporal-status", "export.visible-list" }
                : new[] { "export.guarantee-report", "export.guarantee-history", "export.same-bank", "export.same-supplier", "export.same-temporal-status" };

            var copyItems = new List<ContextActionDefinition>();
            copyItems.AddRange(ContextActionMenuFactory.FindActionsByIds(
                sourceSections,
                "copy.guarantee-no",
                "copy.supplier",
                "copy.reference-type",
                "copy.reference-number"));
            var executeItems = ContextActionMenuFactory.FindActionsByIds(
                sourceSections,
                "execute.create-extension",
                "execute.create-reduction",
                "execute.create-release",
                "execute.create-liquidation",
                "execute.create-verification",
                "execute.create-replacement",
                "execute.create-annulment",
                "execute.edit-guarantee").ToArray();

            var sections = new List<ContextActionSection>
            {
                new ContextActionSection("افتح", "تنقل سريع إلى ملف الضمان وملفاته المرجعية.", openItems.ToArray())
            };

            if (inquirySection != null)
            {
                sections.Add(inquirySection);
            }

            sections.Add(new ContextActionSection("نفّذ", "أنشئ طلب تشغيل مباشر لهذا الضمان.", executeItems));
            sections.Add(new ContextActionSection("صدّر", "استخراج التقرير أو نفس السياق المرتبط بالضمان المحدد.", ContextActionMenuFactory.FindActionsByIds(sourceSections, exportActionIds).ToArray()));
            sections.Add(new ContextActionSection("انسخ", "نسخ القيم الأساسية من الصف الحالي.", copyItems.ToArray()));

            return sections;
        }

        public static IReadOnlyList<ContextActionSection> BuildRequestSections(IContextActionService contextActionService)
        {
            IReadOnlyList<ContextActionSection> sourceSections = contextActionService.GetWorkflowRequestActions();

            var copyItems = new[]
            {
                ContextActionDefinition.Action(
                    "workspace.request.copy-guarantee-no",
                    "نسخ رقم الضمان",
                    ContextActionResultKind.Clipboard,
                    "ينسخ رقم الضمان المرتبط بالطلب الحالي."),
                ContextActionDefinition.Action(
                    "workspace.request.copy-supplier",
                    "نسخ اسم المورد",
                    ContextActionResultKind.Clipboard,
                    "ينسخ اسم المورد المرتبط بالطلب الحالي.")
            };

            return new[]
            {
                new ContextActionSection(
                    "نفّذ",
                    "إجراء مباشر على الطلب المحدد.",
                    ContextActionMenuFactory.FindActionsByIds(sourceSections, "request.record-response").ToArray()),
                new ContextActionSection(
                    "افتح",
                    "افتح الضمان أو الملفات المرتبطة بهذا الطلب.",
                    ContextActionMenuFactory.FindActionsByIds(sourceSections, "request.open-current-guarantee", "request.open-history", "request.open-letter", "request.open-response").ToArray()),
                new ContextActionSection(
                    "صدّر",
                    "استخراج ملف من نفس نوع الطلب المحدد.",
                    ContextActionMenuFactory.FindActionsByIds(sourceSections, "request.export-pending-same-type", "request.export-pending-extension", "request.export-pending-reduction", "request.export-pending-release", "request.export-pending-liquidation", "request.export-pending-verification", "request.export-pending-replacement").ToArray()),
                new ContextActionSection(
                    "انسخ",
                    "نسخ القيم الأساسية من الصف الحالي.",
                    copyItems)
            };
        }
    }
}
