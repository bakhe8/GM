# الحالة الحالية المختصرة — GuaranteeManager

**تاريخ التحديث:** 2026-04-26  
**هذا الملف هو أسرع مرجع لمعرفة أين نقف الآن.**

## الجواب السريع

- baseline المنشور للمستخدمين: `v1.0.0`
- النسخة التطويرية الحالية على هذه الشجرة: `GuaranteeManager v1.1.0-preview.2`
- الفرع النشط: `feature/v1.1-operational-polish`
- الواجهات القديمة أصبحت **أرشيفًا مرجعيًا فقط** تحت `archive/v1_views`
- الواجهة الحالية تحت `Presentation/` هي الواجهة الرسمية العاملة

## حالة المنتج الآن

- لا توجد فجوات وظيفية مؤكدة مفتوحة حاليًا بين الواجهات القديمة والجديدة
- مساحات العمل الأساسية والحوارات الرئيسية كلها مزروعة على البنية الجديدة
- المنطق الوظيفي صار قويًا بما يكفي للانتقال من مرحلة parity إلى مرحلة التثبيت

## الأولوية الحالية

بعد إقفال جولة stabilization الأساسية ورفع الشجرة إلى `preview.2`، صارت الأولوية الحالية هي:

1. **Controlled UAT**
2. **Real-data validation**
3. **Performance and usability polish**

كما أضيف الآن مرجع حي لمراجعة نضج السطوح الأساسية الأربع مقابل التوجيه المستخرج من الأرشيف:

- [guides/Focused_Surface_Review_From_Archive.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Focused_Surface_Review_From_Archive.md:1)

أي أن السؤال الحالي لم يعد: "هل الشجرة نفسها منضبطة؟"  
بل: "هل هذه baseline النظيفة تصمد تحت الاستخدام الواقعي؟"

## آخر تحقق معروف

- `dotnet build .\\my_work.sln` ناجح
- `dotnet test .\\my_work.sln --no-build` ناجح: `50/50`
- baseline القبول الحالي محفوظ هنا:
  - [UIAcceptance/baselines/2026-04-26-stabilization](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-stabilization)
- كما توجد جولة قبول ناجحة على بيانات مولدة أثقل هنا:
  - [UIAcceptance/baselines/2026-04-26-heavy-seed-acceptance](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-heavy-seed-acceptance)
- كما توجد جولة UAT أحدث على baseline `preview.2` شددت حسم نوافذ التأكيد وأعادت مساري `NewGuaranteeDiscard` و`All` إلى النجاح:
  - [UIAcceptance/baselines/2026-04-26-uat-pass-04.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-uat-pass-04.md)
- مجلد `UIAcceptance/latest/` يبقى generated وتشغيليًا فقط، وليس baseline رسمية

## وضع التوثيق

- مصادر الحقيقة الرسمية الآن موضحة في [README.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/README.md:1)
- الأدلة التشغيلية تبقى داخل `Doc/guides/`
- المواد التخطيطية وUX القديمة تبقى داخل `Doc/archive/`

## حالة ملف الفجوات

- الملف الرسمي الوحيد لأي فجوة لاحقة هو:
  - [missing_features_report.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/missing_features_report.md:1)
- حالته الحالية: **لا توجد فجوات مؤكدة مفتوحة**

## أين أبدأ القراءة

- [README.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/README.md:1)
  - لمعرفة الوثائق الرسمية وما هو أرشيف
- [CURRENT_STATE.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md:1)
  - للوضع الحالي المختصر
- [missing_features_report.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/missing_features_report.md:1)
  - لمعرفة هل ظهر backlog جديد أم لا
- [UIAcceptance/baselines/2026-04-26-stabilization](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-stabilization)
  - للـ baseline المعتمد حاليًا
- [README_v1.1.0-preview.2.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/releases/README_v1.1.0-preview.2.md:1)
  - لوضع النسخة الحالية كإصدار تطويري
