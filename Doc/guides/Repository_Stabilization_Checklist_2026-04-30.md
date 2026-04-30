# Repository Stabilization Checklist - 2026-04-30

الغرض من هذه القائمة هو إيقاف تمدد العمل الحالي وتحويله إلى حالة مستودع قابلة للمراجعة والدمج والإصدار.

## الحكم الحالي

- الفرع النشط: `feature/v1.1-operational-polish`
- الفرع متقدم محليًا عن `origin/feature/v1.1-operational-polish` بعدد كبير من commits.
- كانت توجد كتلة WIP واسعة تشمل منطق workflow، الواجهات، التوليد، الاختبارات، والتوثيق، وتم تحويلها إلى اعتمادات stabilization واضحة.
- النسخة المناسبة لهذه الكتلة هي `v1.1.0-preview.3` وليست `rc.1` بعد.

## ما لا نفعله الآن

- لا نضيف ميزات جديدة قبل تنظيف الشجرة.
- لا ندمج إلى `main` قبل التحقق النهائي من الاعتمادات الحالية.
- لا نوسم `rc.1` قبل UAT مختصر على نسخة منشورة.
- لا نضيف تقارير Excel مولدة إلى git.

## تقسيم commits المعتمد

1. **Workflow lifecycle and workspaces**
   - إنهاء الإفراج والتسييل دون إصدار جديد.
   - الاستبدال كضمان بديل.
   - إزالة مسار النقض.
   - حماية قراءة بيانات `Annulment` القديمة.
   - تحديث `DataSeedingService`.
   - إضافة `WorkflowRuleInvariantTests`.
   - ضمان عدم توليد `Annulment`.
   - تسميات الإصدار الأساس/الناتج/المرتبط.
   - تحديث تصديرات وتقارير الطلبات والضمانات.
   - ترقيم الصفحات.
   - إعادة توزيع أفعال الصفوف والألواح.
   - تنظيف مسارات `Requests`, `Guarantees`, `Dashboard`, `Banks`, `Reports`, `Settings`.

2. **UI automation tooling**
   - تحديث SmokeNavigation مع السايدبار الحالية.
   - تحويل مسار طباعة سجل الضمان إلى شاشة `Reports`.
   - تجاهل تقارير Excel المولدة في جذر المشروع.

3. **Documentation and release metadata**
   - `v1.1.0-preview.3`.
   - `AI_HANDOFF.md`.
   - `CURRENT_STATE.md`.
   - release notes.

## بوابة التحقق قبل الدمج

- `dotnet test .\my_work.sln`
- `dotnet test -c Release .\my_work.sln`
- `git diff --check`
- مراجعة `git status --short` والتأكد من عدم وجود artifacts.
- تشغيل smoke يدوي مختصر:
  - إنشاء طلب تمديد.
  - تنفيذ إفراج.
  - تنفيذ تسييل.
  - تنفيذ استبدال.
  - توليد بيانات تجريبية ثم فتح `Requests` و`Guarantees`.

## قرار الانتقال إلى RC

ننتقل إلى `v1.1.0-rc.1` فقط بعد أن تصبح الشجرة نظيفة ويثبت أن `preview.3` يعمل كحزمة مراجعة مستقرة.
