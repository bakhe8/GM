using GuaranteeManager.Models;

namespace GuaranteeManager.Utils
{
    public static class ContextActionIconResolver
    {
        public static string ResolveGeometryKey(ContextActionDefinition action)
        {
            string searchText = $"{action.Id} {action.Header} {action.Description}".ToLowerInvariant();

            if (action.IsDestructive || ContainsAny(searchText, "حذف", "delete", "remove", "exclude", "استبعاد"))
            {
                return "Icon_Geometry_Delete";
            }

            if (ContainsAny(searchText, "attachment", "attachments", "مرفق", "مرفقات", "file list", "الملفات الداعمة"))
            {
                return "Icon_Geometry_Attachment";
            }

            if (ContainsAny(searchText, "history", "timeline", "سجل", "تاريخ"))
            {
                return "Icon_Geometry_History";
            }

            if (ContainsAny(searchText, "print", "طباعة"))
            {
                return "Icon_Geometry_Print";
            }

            if (ContainsAny(searchText, "export", "تصدير", "report", "تقرير"))
            {
                return "Icon_Geometry_Output";
            }

            if (ContainsAny(searchText, "edit", "تحرير", "update", "تعديل"))
            {
                return "Icon_Geometry_Edit";
            }

            if (ContainsAny(searchText, "create", "new", "add", "start", "begin", "إنشاء", "إضافة", "ابدأ", "بدء"))
            {
                return "Icon_Geometry_Add";
            }

            if (ContainsAny(searchText, "copy", "نسخ"))
            {
                return "Icon_Geometry_Edit";
            }

            return action.ResultKind switch
            {
                ContextActionResultKind.ExternalDocument => "Icon_Geometry_Open",
                ContextActionResultKind.ManagedReferenceWindow => "Icon_Geometry_Open",
                ContextActionResultKind.Navigation => "Icon_Geometry_Open",
                ContextActionResultKind.InsightWindow => "Icon_Geometry_Advisor",
                ContextActionResultKind.Export => "Icon_Geometry_Output",
                ContextActionResultKind.Clipboard => "Icon_Geometry_Edit",
                ContextActionResultKind.DecisionDialog => "Icon_Geometry_Confirm",
                _ => "Icon_Geometry_Open"
            };
        }

        public static string ResolveSectionGeometryKey(string header, string description)
        {
            string searchText = $"{header} {description}".ToLowerInvariant();

            if (ContainsAny(searchText, "افتح", "فتح", "files", "documents"))
            {
                return "Icon_Geometry_Open";
            }

            if (ContainsAny(searchText, "صدّر", "تصدير", "report", "output"))
            {
                return "Icon_Geometry_Output";
            }

            if (ContainsAny(searchText, "نسخ", "copy"))
            {
                return "Icon_Geometry_Edit";
            }

            if (ContainsAny(searchText, "افهم", "جواب", "دليل"))
            {
                return "Icon_Geometry_Advisor";
            }

            if (ContainsAny(searchText, "نفّذ", "نفذ", "تعامل", "قرار"))
            {
                return "Icon_Geometry_Confirm";
            }

            return "Icon_Geometry_Open";
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            foreach (string value in values)
            {
                if (source.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
