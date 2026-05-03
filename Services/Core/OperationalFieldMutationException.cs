using System;
using System.Collections.Generic;
using System.Linq;

namespace GuaranteeManager.Services
{
    public sealed class OperationalFieldMutationException : InvalidOperationException
    {
        public OperationalFieldMutationException(IEnumerable<string> fieldNames)
            : base(BuildMessage(fieldNames))
        {
            FieldNames = fieldNames?.ToArray() ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> FieldNames { get; }

        private static string BuildMessage(IEnumerable<string>? fieldNames)
        {
            string fields = string.Join("، ", fieldNames ?? Array.Empty<string>());
            if (string.IsNullOrWhiteSpace(fields))
            {
                fields = "حقول تشغيلية";
            }

            return $"التعديل الإداري لا يسمح بتغيير الحقول التشغيلية التالية: {fields}. استخدم مسارات الطلبات المعتمدة لتغيير بيانات دورة حياة الضمان.";
        }
    }
}
