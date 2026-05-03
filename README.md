# GuaranteeManager

نظام داخلي لإدارة الضمانات البنكية لصالح `مستشفى الملك فيصل التخصصي ومركز الأبحاث`.

## الحالة الحالية

- الإصدار الحالي: `v1.1.0-rc.5`
- الفرع النشط: `feature/v1.1-operational-polish`
- الواجهات الرسمية: `اليوم`، `الضمانات`، `البنوك`، `التقارير`، `الإعدادات`
- السجل الزمني داخل الضمان هو مصدر الحقيقة لدورة حياة الضمان والطلبات وردود البنك والمرفقات.

## التشغيل والتحقق

```powershell
dotnet build GuaranteeManager.csproj
dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj
.\scripts\publish_release.ps1
```

## التوثيق الرسمي

- `AI_HANDOFF.md`
- `Doc/CURRENT_STATE.md`
- `Doc/README.md`
- `Doc/git_workflow.md`
- `Doc/guides/Repository_Structure.md`
- `Doc/releases/README_v1.1.0-rc.5.md`
- `Doc/design/Visual_Identity.md`
- `Doc/audits/Missing_Features_Report.md`

## سياسة الملفات

- لا تحفظ مخرجات Excel أو لقطات التشغيل في الجذر.
- مخرجات التجارب المؤقتة مكانها `scratch/`.
- حزم النشر ومجلدات الإصدار مكانها `releases/` وهي غير متتبعة في git.
- الوثائق القديمة أو غير اليومية مكانها `Doc/archive/`.
