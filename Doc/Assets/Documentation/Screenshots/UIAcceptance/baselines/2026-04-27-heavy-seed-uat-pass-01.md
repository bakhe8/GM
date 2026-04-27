# Heavy-Seed UAT Pass 01

**التاريخ:** 2026-04-27  
**النسخة:** `v1.1.0-preview.2`  
**الهدف:** تشغيل UAT أوسع على بيانات مولدة من داخل `Settings` ثم الحكم على البطء المتبقي: هل مصدره التطبيق أم الأداة؟

## ما الذي اختبرناه

1. تشغيل التطبيق على جلسة جديدة
2. الانتقال إلى `الإعدادات`
3. `أدوات -> توليد بيانات تجريبية`
4. قبول `تأكيد توليد البيانات`
5. العودة إلى `الضمانات`
6. تشغيل `عرض الجواب التشغيلي` من ملف الضمان

## ما الذي انكشف

في المحاولة الأولى بعد `seed-development-data` ظهر خلل حقيقي في التطبيق:

- `Shell` احتفظت بـ `SelectedGuarantee` قديم لم يعد موجودًا بعد إعادة بناء قاعدة البيانات
- النتيجة: بدل فتح `OperationalInquiryDialog` ظهرت رسالة:
  - `تعذر العثور على الضمان المحدد.`

هذه لم تكن مشكلة أداة، بل مشكلة **حالة جلسة stale بعد reseed**.

## الإصلاح

أُضيف refresh مركزي بعد نجاح `SeedDevelopmentData`:

- `SettingsWorkspaceSurface` لم تعد تكتفي بـ `ApplyFilters()` المحلي
- بل تستدعي callback يعيد بناء snapshot داخل `Shell`
- هذا المسار الجديد:
  - يحمّل الفلاتر من جديد
  - يعيد `Refresh()`
  - يختار ضمانًا صالحًا من البيانات الجديدة
  - ويمسح `LastFile` إذا لم يعد موجودًا

## كيف تحققنا

بعد الإصلاح أُعيد نفس السيناريو حرفيًا:

- `Settings -> توليد بيانات تجريبية -> Yes -> الضمانات -> عرض الجواب التشغيلي`

والنتيجة:

- `ShellState.Reason = data-reset`
- `SelectedGuaranteeId` تغير من الكيان القديم إلى كيان جديد صالح
- `OperationalInquiryDialog` فتحت بنجاح
- وسُجلت الأحداث:
  - `guarantee.inquiry completed`
  - `dialog.secondary open`
  - `dialog.window loaded`

## الحكم على الأداء

هذه الجولة وضحت الصورة أكثر:

### بطء يغلب عليه أنه من التطبيق

- اختيار `توليد بيانات تجريبية` من القائمة ثم ظهور `تأكيد توليد البيانات`
  - ظهر في timeline كـ `Click = 6444ms`
  - هذا ليس زمن نقرة خام، بل زمن انتقال التطبيق من:
    - قائمة منبثقة
    - إلى dialog تأكيد modal
- `DialogAction` على `Yes`
  - سجلت `2504ms`
  - وهذا طبيعي نسبيًا لأن التطبيق بعد التأكيد يبدأ عملية reseed فعلية

### كلفة ما زالت أقرب للأداة/الاستكشاف

- البحث عن عنصر القائمة المنبثقة:
  - `Elements = 1370ms`
  - هذه كلفة UIA discovery داخل popup/menu أكثر من كونها bottleneck منتج

### بطء حدودي لكنه مقبول

- `Sidebar`
  - بين `1383ms` و`1481ms`
  - قريب من العتبة لكنه ليس بطيئًا جدًا
- `Click` على `عرض الجواب التشغيلي` بعد الإصلاح
  - `929ms`
  - أعلى قليلًا من العتبة المقبولة لكنه ليس بطئًا شديدًا

## الخلاصة

- الجولة كشفت **bug حقيقيًا** بعد reseed وأُغلق
- بعد الإغلاق، صمد المسار نفسه وفتح `OperationalInquiryDialog` بنجاح
- الحكم الحالي:
  - البطء الأوضح المتبقي في هذا المسار هو **بطء تطبيق/تدفق UI** حول:
    - القوائم المنبثقة
    - نوافذ التأكيد
    - العمليات المدمرة مثل reseed
  - بينما كلفة الأداة الباقية تتركز أكثر في:
    - اكتشاف عناصر popup/menu عبر UI Automation

## مراجع الجولة

- [CURRENT_STATE.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md:1)
- [Preview2_Polish_Backlog.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Preview2_Polish_Backlog.md:1)
- [performance-pass-01](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-performance-pass-01.md:1)
