using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public class WorkflowLetterService
    {
        public GeneratedWorkflowFile CreateExtensionLetter(Guarantee guarantee, DateTime requestedExpiryDate, string notes, string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_تمديد_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب تمديد ضمان بنكي",
                guarantee,
                ("تاريخ الانتهاء الحالي", FormatGuaranteeDate(guarantee, guarantee.ExpiryDate)),
                ("التاريخ المطلوب بعد التمديد", FormatGuaranteeDate(guarantee, requestedExpiryDate)),
                notes,
                createdBy,
                "نأمل التكرم بتمديد الضمان المشار إليه أعلاه حتى التاريخ الجديد الموضح في هذا الخطاب، مع الإبقاء على بقية بيانات الضمان دون تغيير ما لم يرد من البنك ما يخالف ذلك."), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public GeneratedWorkflowFile CreateReductionLetter(Guarantee guarantee, decimal requestedAmount, string notes, string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_تخفيض_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب تخفيض ضمان بنكي",
                guarantee,
                ("المبلغ الحالي", FormatLetterAmount(guarantee.Amount)),
                ("المبلغ المطلوب بعد التخفيض", FormatLetterAmount(requestedAmount)),
                notes,
                createdBy,
                "نأمل التكرم بتخفيض مبلغ الضمان المشار إليه أعلاه إلى القيمة الموضحة في هذا الخطاب، مع الإبقاء على بقية بيانات الضمان دون تغيير ما لم يرد من البنك ما يخالف ذلك."), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public GeneratedWorkflowFile CreateReleaseLetter(Guarantee guarantee, string notes, string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_افراج_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب إفراج عن ضمان بنكي",
                guarantee,
                ("حالة الضمان الحالية", guarantee.LifecycleStatusLabel),
                ("الإجراء المطلوب", "إفراج"),
                notes,
                createdBy,
                "نأمل التكرم بالإفراج عن الضمان المشار إليه أعلاه وإنهاء التزامه وفق ما يرد في مستند رد البنك الرسمي."), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public GeneratedWorkflowFile CreateLiquidationLetter(Guarantee guarantee, string notes, string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_تسييل_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب تسييل أو مصادرة ضمان بنكي",
                guarantee,
                ("حالة الضمان الحالية", guarantee.LifecycleStatusLabel),
                ("الإجراء المطلوب", "تسييل أو مصادرة"),
                notes,
                createdBy,
                "نأمل التكرم باتخاذ ما يلزم لتسييل الضمان المشار إليه أعلاه أو مصادرته، وفق ما يثبت في مستند رد البنك الرسمي، على أن يعتمد الأثر النهائي بعد تسجيل الاستجابة الرسمية في النظام."), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public GeneratedWorkflowFile CreateVerificationLetter(Guarantee guarantee, string notes, string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_تحقق_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب تحقق من ضمان بنكي",
                guarantee,
                ("الحالة الحالية", guarantee.LifecycleStatusLabel),
                ("الإجراء المطلوب", "تحقق من سريان الضمان أو صحة بياناته"),
                notes,
                createdBy,
                "نأمل التكرم بتزويدنا بإفادة رسمية عن وضع الضمان المشار إليه أعلاه، بما يثبت سريانه أو يوضح أي بيانات رسمية مرتبطة به وفق مستند رد البنك."), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public GeneratedWorkflowFile CreateReplacementLetter(
            Guarantee guarantee,
            string replacementGuaranteeNo,
            decimal replacementAmount,
            DateTime replacementExpiryDate,
            GuaranteeDateCalendar replacementDateCalendar,
            string notes,
            string createdBy)
        {
            AppPaths.EnsureDirectoriesExist();

            string originalFileName = FileHelper.SanitizeFileName($"طلب_استبدال_{guarantee.GuaranteeNo}_{DateTime.Now:yyyyMMdd}.html");
            string savedFileName = FileHelper.GenerateUniqueFileName(".html");
            string fullPath = Path.Combine(AppPaths.WorkflowLettersFolder, savedFileName);

            string bodyText =
                $"نأمل التكرم باستبدال الضمان المشار إليه أعلاه بضمان بديل رقم {replacementGuaranteeNo}." +
                $"{Environment.NewLine}مبلغ الضمان البديل المطلوب: {FormatLetterAmount(replacementAmount)}" +
                $"{Environment.NewLine}تاريخ انتهاء الضمان البديل: {DualCalendarDateService.FormatDate(replacementExpiryDate, replacementDateCalendar)}" +
                $"{Environment.NewLine}ويُعتمد التنفيذ النهائي وفق ما يرد في مستند رد البنك.";

            File.WriteAllText(fullPath, BuildRequestLetterHtml(
                "خطاب طلب استبدال ضمان بنكي",
                guarantee,
                ("رقم الضمان الحالي", guarantee.GuaranteeNo),
                ("رقم الضمان البديل", replacementGuaranteeNo),
                notes,
                createdBy,
                bodyText), Encoding.UTF8);

            return new GeneratedWorkflowFile
            {
                OriginalFileName = originalFileName,
                SavedFileName = savedFileName,
                FullPath = fullPath
            };
        }

        public void OpenLetter(WorkflowRequest request)
        {
            OpenFile(request.LetterFilePath);
        }

        public void OpenResponseDocument(WorkflowRequest request)
        {
            OpenFile(request.ResponseFilePath);
        }

        public void OpenFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException("الملف المطلوب غير موجود.", fullPath);
            }

            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
        }

        private static string BuildRequestLetterHtml(
            string title,
            Guarantee guarantee,
            (string Label, string Value) currentValue,
            (string Label, string Value) requestedValue,
            string notes,
            string createdBy,
            string bodyText)
        {
            string safeNotes = string.IsNullOrWhiteSpace(notes) ? "لا توجد ملاحظات إضافية." : System.Net.WebUtility.HtmlEncode(notes);
            string safeTitle = System.Net.WebUtility.HtmlEncode(title);
            string safeGuaranteeNo = System.Net.WebUtility.HtmlEncode(guarantee.GuaranteeNo);
            string safeSupplier = EncodeLetterText(guarantee.Supplier);
            string safeBank = EncodeLetterText(guarantee.Bank);
            string safeGuaranteeType = EncodeLetterText(guarantee.GuaranteeType);
            string safeReferenceType = EncodeLetterText(guarantee.ReferenceTypeLabel);
            string safeReferenceNumber = EncodeLetterText(guarantee.ReferenceNumber);
            string safeBeneficiary = EncodeLetterText(BusinessPartyDefaults.NormalizeBeneficiary(guarantee.Beneficiary));
            string safeGuaranteeAmount = EncodeLetterText(FormatLetterAmount(guarantee.Amount));
            string safeGuaranteeExpiry = EncodeLetterText(FormatGuaranteeDate(guarantee, guarantee.ExpiryDate));
            string safeCurrentLabel = System.Net.WebUtility.HtmlEncode(currentValue.Label);
            string safeCurrentValue = EncodeLetterText(currentValue.Value);
            string safeRequestedLabel = System.Net.WebUtility.HtmlEncode(requestedValue.Label);
            string safeRequestedValue = EncodeLetterText(requestedValue.Value);
            string safeBodyText = System.Net.WebUtility.HtmlEncode(bodyText);
            string safeCreatedBy = EncodeLetterText(createdBy);

            return $@"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""utf-8"" />
    <title>{safeTitle}</title>
    <style>
        body {{
            font-family: Tahoma, Arial, sans-serif;
            margin: 32px;
            color: #111111;
            line-height: 1.9;
            background: #ffffff;
            font-size: 15px;
        }}
        .header {{
            border-bottom: 2px solid #006847;
            padding-bottom: 14px;
            margin-bottom: 20px;
        }}
        .title {{
            font-size: 24px;
            font-weight: bold;
            margin-bottom: 8px;
        }}
        .meta {{
            font-size: 13px;
            color: #666666;
        }}
        .recipient {{
            margin: 18px 0 10px;
        }}
        .subject {{
            margin: 10px 0 18px;
            padding: 10px 14px;
            background: #f7f7f7;
            border: 1px solid #cfcfcf;
            border-radius: 10px;
            font-weight: 700;
        }}
        .section-title {{
            font-size: 15px;
            font-weight: 700;
            margin: 18px 0 8px;
            color: #111111;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin: 8px 0 18px;
        }}
        th, td {{
            border: 1px solid #cfcfcf;
            padding: 10px 12px;
            text-align: right;
            vertical-align: top;
        }}
        td {{
            white-space: pre-line;
        }}
        th {{
            background: #f2f2f2;
            width: 24%;
            font-weight: 600;
        }}
        .body-text {{
            white-space: pre-line;
            border: 1px solid #cfcfcf;
            border-radius: 10px;
            padding: 14px 16px;
            background: #ffffff;
        }}
        .notes-box {{
            margin-top: 12px;
            white-space: pre-line;
            border: 1px dashed #666666;
            border-radius: 10px;
            padding: 12px 14px;
            background: #f7f7f7;
        }}
        .closing {{
            margin-top: 18px;
        }}
        .footer {{
            margin-top: 36px;
            font-size: 13px;
            color: #666666;
            border-top: 1px solid #cfcfcf;
            padding-top: 12px;
        }}
        @media print {{
            body {{ margin: 18px; }}
            .subject, .body-text, .notes-box {{
                break-inside: avoid;
            }}
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <div class=""title"">{safeTitle}</div>
        <div class=""meta"">تاريخ إنشاء الخطاب: {FormatGuaranteeDate(guarantee, DateTime.Now)} | جهة الإصدار: نظام إدارة الضمانات البنكية</div>
    </div>

    <div class=""recipient"">
        السادة/ {safeBank} المحترمين<br />
        السلام عليكم ورحمة الله وبركاته،
    </div>

    <div class=""subject"">الموضوع: {safeTitle}</div>

    <div class=""section-title"">بيانات الضمان</div>
    <table>
        <tr><th>رقم الضمان</th><td>{safeGuaranteeNo}</td></tr>
        <tr><th>المورد</th><td>{safeSupplier}</td></tr>
        <tr><th>البنك</th><td>{safeBank}</td></tr>
        <tr><th>نوع الضمان</th><td>{safeGuaranteeType}</td></tr>
        <tr><th>المستفيد</th><td>{safeBeneficiary}</td></tr>
        <tr><th>نوع المرجع</th><td>{safeReferenceType}</td></tr>
        <tr><th>رقم المرجع</th><td>{safeReferenceNumber}</td></tr>
        <tr><th>المبلغ</th><td>{safeGuaranteeAmount}</td></tr>
        <tr><th>تاريخ انتهاء الضمان</th><td>{safeGuaranteeExpiry}</td></tr>
        <tr><th>{safeCurrentLabel}</th><td>{safeCurrentValue}</td></tr>
        <tr><th>{safeRequestedLabel}</th><td>{safeRequestedValue}</td></tr>
    </table>

    <div class=""section-title"">تفاصيل الطلب</div>
    <div class=""body-text"">
        {safeBodyText}
    </div>

    <div class=""section-title"">ملاحظات إضافية</div>
    <div class=""notes-box"">{safeNotes}</div>

    <div class=""closing"">
        وتفضلوا بقبول فائق التحية والتقدير.
    </div>

    <div class=""footer"">
        أُنشئ هذا الخطاب بواسطة: {safeCreatedBy}<br />
        هذا الخطاب مخصص للمتابعة التشغيلية الداخلية وحفظ المرجع الرسمي للطلب.
    </div>
</body>
</html>";
        }

        private static string EncodeLetterText(string? value)
        {
            return System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "---" : value);
        }

        private static string FormatLetterAmount(decimal amount)
        {
            return ArabicAmountFormatter.FormatSaudiRiyalsForLetter(amount);
        }

        private static string FormatGuaranteeDate(Guarantee guarantee, DateTime date)
        {
            return DualCalendarDateService.FormatDate(date, guarantee.DateCalendar);
        }
    }

    public class GeneratedWorkflowFile
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public string SavedFileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }
}
