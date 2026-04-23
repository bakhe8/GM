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
                    "افهم",
                    "أسئلة وجوابات تساعد على فهم وضع هذا الضمان أو ما يرتبط به من بنك ومورد وملخصات تشغيلية.",
                    ContextActionDefinition.Group(
                        "هذا الضمان",
                        "جوابات مباشرة عن حالة الضمان نفسه.",
                        ContextActionDefinition.Action("guarantee.last-event", "ما آخر ما حدث لهذا الضمان؟", ContextActionResultKind.InsightWindow, "يفتح جوابًا مباشرًا مدعومًا بالأدلة عن آخر حدث على هذا الضمان."),
                        ContextActionDefinition.Action("guarantee.extension-timing", "هل طلبنا تمديده قبل الانتهاء؟", ContextActionResultKind.InsightWindow, "يفتح جوابًا يقارن توقيت طلب التمديد بتاريخ الانتهاء."),
                        ContextActionDefinition.Action("guarantee.outstanding-extension", "لماذا لم يتم تمديده حتى الآن؟", ContextActionResultKind.InsightWindow, "يفتح سبب عدم تنفيذ التمديد حتى الآن مع الأدلة."),
                        ContextActionDefinition.Action("guarantee.outstanding-release", "لماذا لم يتم الإفراج عنه حتى الآن؟", ContextActionResultKind.InsightWindow, "يفتح سبب عدم تنفيذ الإفراج حتى الآن مع الأدلة."),
                        ContextActionDefinition.Action("guarantee.outstanding-liquidation", "لماذا لم يتم تسييله حتى الآن؟", ContextActionResultKind.InsightWindow, "يفتح سبب عدم تنفيذ التسييل حتى الآن مع الأدلة."),
                        ContextActionDefinition.Action("guarantee.expired-without-extension", "لماذا انتهى هذا الضمان ولم يُمدد؟", ContextActionResultKind.InsightWindow, "يفتح تفسيرًا لحالة الضمان المنتهي دون تمديد منفذ."),
                        ContextActionDefinition.Action("guarantee.release-evidence", "لماذا تم الإفراج عن هذا الضمان وعلى أي مستند؟", ContextActionResultKind.InsightWindow, "يفتح أساس الإفراج والمستندات التي دعمت القرار."),
                        ContextActionDefinition.Action("guarantee.liquidation-evidence", "لماذا تم تسييل هذا الضمان وعلى أي مستند؟", ContextActionResultKind.InsightWindow, "يفتح أساس التسييل والمستندات التي دعمت القرار."),
                        ContextActionDefinition.Action("guarantee.reduction-source", "أين طلب التخفيض الذي نتج عنه هذا الأثر؟", ContextActionResultKind.InsightWindow, "يفتح الطلب المرتبط بالأثر الحالي على مبلغ الضمان."),
                        ContextActionDefinition.Action("guarantee.response-link-status", "هل يوجد مستند رد محفوظ بدون ربط رسمي؟", ContextActionResultKind.InsightWindow, "يفتح حالة ربط مستندات رد البنك بالسجل الرسمي للضمان.")),
                    ContextActionDefinition.Group(
                        "البنك والمورد",
                        "جوابات ترتبط بالبنك أو المورد الموجودين في الصف الحالي.",
                        ContextActionDefinition.Action("bank.pending-requests", "ما الطلبات المعلقة لدى هذا البنك؟", ContextActionResultKind.InsightWindow, "يفتح ملخص الطلبات المعلقة لدى البنك الحالي مع إمكانية التصدير."),
                        ContextActionDefinition.Action("bank.confirmation", "هل أكد هذا البنك كل طلباتنا؟", ContextActionResultKind.InsightWindow, "يفتح ملخصًا لوضع استجابة هذا البنك لكل الطلبات المرتبطة به."),
                        ContextActionDefinition.Action("supplier.latest-activity", "ما آخر ما حدث لضمانات هذا المورد؟", ContextActionResultKind.InsightWindow, "يفتح آخر نشاط تشغيلي على ضمانات المورد الحالي.")),
                    ContextActionDefinition.Group(
                        "ملخصات سريعة",
                        "جوابات مجمعة سريعة مرتبطة بالسياق الحالي.",
                        ContextActionDefinition.Action("summary.executed-extensions-this-month", "كم ضمان قمنا بتمديده هذا الشهر؟", ContextActionResultKind.InsightWindow, "يفتح ملخص تمديدات هذا الشهر مع إمكانية التصدير."),
                        ContextActionDefinition.Action("summary.active-po-only", "كم لدينا من ضمانات سارية تخص أوامر الشراء فقط؟", ContextActionResultKind.InsightWindow, "يفتح ملخص الضمانات السارية المرتبطة بأوامر الشراء فقط."),
                        ContextActionDefinition.Action("summary.contract-released-last-week", "كم عدد الضمانات الخاصة بالعقود التي أفرجنا عنها خلال الأسبوع الفائت؟", ContextActionResultKind.InsightWindow, "يفتح ملخص الإفراجات المرتبطة بالعقود خلال آخر أسبوع."),
                        ContextActionDefinition.Action("summary.oldest-pending", "ما أكثر 10 طلبات تأخر ردها؟", ContextActionResultKind.InsightWindow, "يفتح قائمة أقدم الطلبات المعلقة التي ما زالت تنتظر الرد."),
                        ContextActionDefinition.Action("summary.expired-po-without-extension", "كم مبلغ ضمانات أوامر الشراء المنتهية بلا تمديد؟", ContextActionResultKind.InsightWindow, "يفتح ملخص الضمانات المنتهية المرتبطة بأوامر الشراء دون تمديد منفذ.")),
                    ContextActionDefinition.Group(
                        "استعلامات أداء الموظفين",
                        "ملخصات تعتمد على اسم الموظف والفترة الزمنية.",
                        ContextActionDefinition.Action("employee.contract-requests-last-month", "كم طلب تمديد أو إفراج أنشأه موظف محدد الشهر الماضي للعقود؟", ContextActionResultKind.InsightWindow, "يفتح ملخص طلبات موظف محدد خلال الشهر الماضي مع إمكانية التصدير."))),

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
                        ContextActionDefinition.Action("execute.create-replacement", "طلب استبدال", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب استبدال للضمان الحالي."),
                        ContextActionDefinition.Action("execute.create-annulment", "طلب نقض", ContextActionResultKind.DecisionDialog, "يفتح نافذة إنشاء طلب نقض للضمان الحالي أو من قائمة الضمانات المؤهلة.")),
                    ContextActionDefinition.Action("execute.edit-guarantee", "تعديل الضمان", ContextActionResultKind.Navigation, "ينتقل إلى شاشة تعديل هذا الضمان.")),

                new ContextActionSection(
                    "افتح الملفات الداعمة",
                    "افتح المرفقات والملفات المرتبطة بهذا الضمان.",
                    ContextActionDefinition.Action("evidence.attachments", "عرض المرفقات", ContextActionResultKind.ManagedReferenceWindow, "يفتح نافذة مرجعية لعرض مرفقات هذا الضمان.")),

                new ContextActionSection(
                    "انتقل",
                    "افتح السجلات والشاشات المرتبطة بهذا الضمان.",
                    ContextActionDefinition.Action("navigate.history", "فتح سجل الضمان", ContextActionResultKind.ManagedReferenceWindow, "يفتح سجل الإصدارات الكامل لهذا الضمان.")),

                new ContextActionSection(
                    "صدّر",
                    "استخرج Excel من نفس السياق الحالي.",
                    ContextActionDefinition.Action("export.visible-list", "تصدير السجلات المعروضة حاليًا", ContextActionResultKind.Export, "يصدر السجلات المعروضة حاليًا كما تظهر في الجدول."),
                    ContextActionDefinition.Action("export.guarantee-report", "تصدير تقرير هذا الضمان", ContextActionResultKind.Export, "يصدر تقرير Excel لهذا الضمان فقط."),
                    ContextActionDefinition.Action("export.guarantee-history", "تصدير تاريخ هذا الضمان", ContextActionResultKind.Export, "يصدر تاريخ الضمان الكامل إلى Excel."),
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
                    "انتقل إلى الضمان أو سجله المرتبط بهذا الطلب.",
                    ContextActionDefinition.Action("request.open-current-guarantee", "فتح الضمان الحالي", ContextActionResultKind.Navigation, "ينتقل إلى الضمان الحالي المرتبط بهذا الطلب."),
                    ContextActionDefinition.Action("request.open-history", "عرض تاريخ الضمان", ContextActionResultKind.ManagedReferenceWindow, "يفتح تاريخ الضمان المرتبط بهذا الطلب.")),

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
