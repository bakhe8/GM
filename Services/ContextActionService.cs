using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public sealed class ContextActionService : IContextActionService
    {
        private static readonly IReadOnlyList<ContextActionSection> GuaranteeActions = BuildGuaranteeActions();
        private static readonly IReadOnlyList<ContextActionSection> WorkflowRequestActions = BuildWorkflowRequestActions();

        public IReadOnlyList<ContextActionSection> GetGuaranteeActions() => GuaranteeActions;

        public IReadOnlyList<ContextActionSection> GetWorkflowRequestActions() => WorkflowRequestActions;

        private static IReadOnlyList<ContextActionSection> BuildGuaranteeActions()
        {
            return new[]
            {
                new ContextActionSection(
                    "نفّذ",
                    "إجراءات مباشرة على هذا الضمان.",
                    ContextActionDefinition.Group(
                        "إنشاء طلب",
                        "إنشاء طلب جديد مرتبط بالضمان الحالي.",
                        ContextActionDefinition.Action("execute.create-extension", "طلب تمديد", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب تمديد للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-reduction", "طلب تخفيض", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب تخفيض للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-release", "طلب إفراج", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب إفراج للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-liquidation", "طلب تسييل", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب تسييل للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-verification", "طلب تحقق", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب تحقق للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-replacement", "طلب استبدال", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب استبدال للضمان الحالي.")),
                    ContextActionDefinition.Action("execute.edit-guarantee", "تعديل الضمان", ContextActionResultKind.Navigation, "ينتقل إلى شاشة تعديل هذا الضمان.")),

                new ContextActionSection(
                    "افتح الملفات الداعمة",
                    "افتح المرفقات والملفات المرتبطة بهذا الضمان.",
                    ContextActionDefinition.Action("evidence.attachments", "عرض المرفقات", ContextActionResultKind.ManagedReferenceWindow, "يفتح نافذة مرجعية لعرض مرفقات هذا الضمان.")),

                new ContextActionSection(
                    "صدّر",
                    "استخرج Excel من نفس السياق الحالي.",
                    ContextActionDefinition.Action("export.visible-list", "تصدير السجلات المعروضة حاليًا", ContextActionResultKind.Export, "يصدر السجلات المعروضة حاليًا كما تظهر في الجدول."),
                    ContextActionDefinition.Action("export.guarantee-report", "تصدير تقرير هذا الضمان", ContextActionResultKind.Export, "يصدر تقرير Excel لهذا الضمان فقط."),
                    ContextActionDefinition.Action("export.same-bank", "تصدير جميع ضمانات نفس البنك", ContextActionResultKind.Export, "يصدر جميع الضمانات الحالية لنفس البنك."),
                    ContextActionDefinition.Action("export.same-supplier", "تصدير جميع ضمانات نفس المورد", ContextActionResultKind.Export, "يصدر جميع الضمانات الحالية لنفس المورد."),
                    ContextActionDefinition.Action("export.same-temporal-status", "تصدير جميع الضمانات بنفس الحالة الزمنية", ContextActionResultKind.Export, "يصدر جميع الضمانات الحالية التي تشترك في نفس الحالة الزمنية.")),

                new ContextActionSection(
                    "انسخ",
                    "انسخ القيم الأساسية من الصف الحالي.",
                    ContextActionDefinition.Action("copy.guarantee-no", "نسخ رقم الضمان", ContextActionResultKind.Clipboard, "ينسخ رقم الضمان إلى الحافظة."),
                    ContextActionDefinition.Action("copy.supplier", "نسخ اسم المورد", ContextActionResultKind.Clipboard, "ينسخ اسم المورد إلى الحافظة."),
                    ContextActionDefinition.Action("copy.reference-type", "نسخ نوع المرجع", ContextActionResultKind.Clipboard, "ينسخ نوع المرجع إلى الحافظة."),
                    ContextActionDefinition.Action("copy.reference-number", "نسخ رقم المرجع", ContextActionResultKind.Clipboard, "ينسخ رقم المرجع إلى الحافظة."))
            };
        }

        private static IReadOnlyList<ContextActionSection> BuildWorkflowRequestActions()
        {
            return new[]
            {
                new ContextActionSection(
                    "نفّذ",
                    "الإجراء التشغيلي المباشر على الطلب المحدد.",
                    ContextActionDefinition.Action("request.record-response", "تسجيل استجابة البنك", ContextActionResultKind.DecisionDialog, "يفتح نافذة تسجيل رد البنك وإغلاق الطلب أو تنفيذه.")),

                new ContextActionSection(
                    "افتح الملفات الداعمة",
                    "افتح خطاب الطلب أو تعامل مع مستند رد البنك لهذا الطلب بحسب حالته.",
                    ContextActionDefinition.Action("request.open-letter", "فتح خطاب الطلب", ContextActionResultKind.ExternalDocument, "يفتح خطاب الطلب في التطبيق الخارجي المناسب."),
                    ContextActionDefinition.Action("request.open-response", "مستند الرد", ContextActionResultKind.ExternalDocument, "يفتح مستند رد البنك إذا كان محفوظًا، أو يتيح إلحاقه للطلبات المغلقة التي نُفذت بلا ملف رد.")),

                new ContextActionSection(
                    "انتقل",
                    "انتقل إلى الضمان المرتبط بهذا الطلب.",
                    ContextActionDefinition.Action("request.open-current-guarantee", "فتح الضمان الحالي", ContextActionResultKind.Navigation, "ينتقل إلى الضمان الحالي المرتبط بهذا الطلب.")),

                new ContextActionSection(
                    "صدّر",
                    "استخرج ملف Excel من نفس نوع الطلب المحدد.",
                    ContextActionDefinition.Action("request.export-pending-same-type", "تصدير المعلقات من نفس النوع", ContextActionResultKind.Export, "يصدر جميع الطلبات المعلقة من نفس نوع الطلب المحدد."),
                    ContextActionDefinition.Action("request.export-pending-extension", "تصدير التمديدات المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات التمديد المعلقة."),
                    ContextActionDefinition.Action("request.export-pending-reduction", "تصدير التخفيضات المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات التخفيض المعلقة."),
                    ContextActionDefinition.Action("request.export-pending-release", "تصدير الإفراجات المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات الإفراج المعلقة."),
                    ContextActionDefinition.Action("request.export-pending-liquidation", "تصدير طلبات التسييل المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات التسييل المعلقة."),
                    ContextActionDefinition.Action("request.export-pending-verification", "تصدير طلبات التحقق المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات التحقق المعلقة."),
                    ContextActionDefinition.Action("request.export-pending-replacement", "تصدير طلبات الاستبدال المعلقة", ContextActionResultKind.Export, "يصدر جميع طلبات الاستبدال المعلقة."))
            };
        }
    }
}
