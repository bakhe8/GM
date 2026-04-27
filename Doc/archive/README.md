# Archive Map

هذا المجلد لم يعد مجرد "مكان قديم".  
هو الآن أرشيف منظم بثلاث طبقات مختلفة:

1. `legacy_ui/`
   - حدود الواجهة القديمة وقواعدها البصرية السابقة
2. `planning/`
   - قرارات تاريخية عن الإطلاق، الفروع، الحالة، والانتقال بين المراحل
3. `ux/`
   - التشخيصات والـ blueprints والقرارات الفكرية التي قادت إعادة البناء

القاعدة المهمة:

- **لا ننفذ مباشرة من الأرشيف**
- لكننا **نستخرج منه ما يزال صالحًا** إلى مراجع حية داخل `Doc/guides/`

المرجع الحي الناتج من هذه المراجعة:

- [Current_Development_Guidance_From_Archive.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Current_Development_Guidance_From_Archive.md:1)

## كيف نقرأ الأرشيف الآن

| القسم | الغرض الحالي | هل يحمل توجيهًا حيًا؟ |
|---|---|---|
| `legacy_ui/` | فهم حدود المرجع القديم وقواعده البصرية السابقة | نعم، جزئيًا |
| `planning/` | فهم القرارات التاريخية، لا قيادة العمل اليومي مباشرة | نادرًا |
| `ux/` | أهم مصدر حي للمبادئ والمسارات والسلوك البصري/التشغيلي | نعم، بقوة |

## تصنيف الملفات

| الملف | النوع | القيمة الحالية | الموقف الحالي |
|---|---|---|---|
| `legacy_ui/README.md` | حدود وأرشفة | يوضح ما الذي أصبح Legacy ولماذا | يبقى مرجع حدود |
| `legacy_ui/design_system_v1_legacy.md` | نظام بصري/تركيبي | ما زال مفيدًا جدًا كمرجع قواعد ومسافات وبنية شاشات | جرى استخراج المبادئ الحية منه |
| `legacy_ui/design_system_v2_reset_archived.md` | قرار تاريخي | يشرح لماذا أُرشفت محاولة reset سابقة | تاريخي فقط |
| `planning/design_system.md` | فهرس قديم | يصف أين كان المرجع سابقًا | تاريخي فقط |
| `planning/merge_summary_v1.1_kickoff.md` | تاريخ فرع | يوثق دفعة دمج سابقة | تاريخي فقط |
| `planning/merge_summary_v1.1_operational_polish.md` | تاريخ فرع | يوثق مسار فرع سابق | تاريخي فقط |
| `planning/project_status.md` | snapshot تاريخي | يشرح حالة المشروع عند لحظة سابقة | تاريخي فقط |
| `planning/release_v1.md` | قرار إصدار | مهم لفهم منطق `v1.0.0` فقط | تاريخي فقط |
| `planning/roadmap_v1.1.md` | خارطة طريق قديمة | يوضح من أين بدأ مسار `v1.1` | تاريخي مع بعض المبادئ المستخرجة |
| `ux/ux-analysis-report.md` | تشخيص UX | ما زال مهمًا لفهم أصل المشاكل التشغيلية | جرى استخراج النتائج الحية |
| `ux/ux-analysis-report2.md` | تشخيص مختصر | يدعم التقرير التشخيصي الأصلي | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_simplification_v1.1.md` | مبادئ اتجاه | ما زال صالحًا جدًا لمرحلة التثبيت الحالية | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_task_architecture_v1.1.md` | IA / task architecture | من أهم الوثائق التي ما زالت صالحة | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_flow_specs_v1.1.md` | flow specs | ما زالت تحمل قرارات تشغيلية مفيدة | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_design_intelligence_v1.1.md` | سلوك ذكي | ما زالت صالحة بقوة لمرحلة polish وUAT | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_visual_intelligence_v1.1.md` | ذكاء بصري | ما زالت صالحة لتقييم dashboard/file/actions | جرى استخلاصه ضمن المرجع الحي |
| `ux/ux_execution_scorecard_v1.1.md` | بطاقة حكم | مهمة كمنهج قبول وجودة، لا كـ backlog قديم | جرى استخلاصها ضمن المرجع الحي |

## القرار التنظيمي الحالي

من الآن:

- لا نعامل كل الأرشيف على أنه "قديم وغير مهم"
- ولا نعيده كله إلى الواجهة الأمامية للمشروع
- بل نقسمه هكذا:

1. **مرجع حي مستخرج**
   - داخل `Doc/guides/Current_Development_Guidance_From_Archive.md`
2. **مرجع حدود**
   - مثل `legacy_ui/README.md`
3. **تاريخ فقط**
   - merge summaries / release snapshots / project status القديم

## متى نرجع للأرشيف الأصلي؟

نرجع إلى الملف الأصلي فقط عندما نحتاج:

- الصياغة الكاملة للقرار
- تاريخ القرار نفسه
- أو نصًا مرجعيًا أوسع من الاستخلاص الحالي

أما العمل اليومي والتطوير الحالي، فيبدأ من:

- [Doc/README.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/README.md:1)
- [CURRENT_STATE.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md:1)
- [Current_Development_Guidance_From_Archive.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Current_Development_Guidance_From_Archive.md:1)
