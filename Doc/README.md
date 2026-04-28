# Documentation Map

هذا المجلد هو المرجع الرسمي لوضع المشروع الحالي، الإصدارات، ونتائج القبول.

## ملفات الحقيقة الرسمية

- `CURRENT_STATE.md`
  - أسرع صورة دقيقة لوضع المشروع الحالي وأولوية العمل الحالية.
- `git_workflow.md`
  - سياسة الفروع، الترقيم، ومعايير الانتقال بين `preview` و`rc` و`release`.
- `releases/README_v1.0.0.md`
  - وصف baseline المنشورة `v1.0.0`.
- `releases/README_v1.1.0-preview.2.md`
  - وصف النسخة التطويرية الحالية.
- `Assets/Documentation/Screenshots/README.md`
  - سياسة لقطات القبول، baselines، ومخرجات الأداة.

## ملفات مساندة خارج هذا المجلد

- `missing_features_report.md`
  - المرجع الرسمي الوحيد لأي فجوة لاحقة بين القديم والجديد.

## أدلة مساندة حية داخل هذا المجلد

- `guides/Current_Development_Guidance_From_Archive.md`
  - استخلاص حي لما يزال صالحًا من وثائق الأرشيف لمرحلة التطوير الحالية.
- `guides/Focused_Surface_Review_From_Archive.md`
  - مراجعة مباشرة للسطوح الحالية الأربع الأهم مقابل هذا التوجيه، مع حكم واضح على ما هو قوي وما هو جزئي.
- `guides/Preview2_Polish_Backlog.md`
  - backlog polish قصيرة ومركزة للنسخة `preview.2` بعد إغلاق parity، مبنية على مراجعة السطوح الحالية.
- `guides/Program_Transition_Plan.md`
  - خطة الانتقال الرسمية من تطوير الأداة إلى تطوير البرنامج الأصلي، مع أول ثلاث ساحات تنفيذ فعلية وقاعدة blocker-driven للأداة.
- `guides/Product_Coverage_Matrix.md`
  - مصفوفة التغطية الحالية للمنتج: ما الذي صار مشبعًا، ومتى نعيد الفحص، ومتى لا تكون الأداة مبررة.
- `guides/Next_Level_Development_Readiness.md`
  - مرجع الجاهزية الرسمي للانتقال إلى مستوى تطوير أعلى في البرنامج، مع نتائج build/tests/regressions والحكم التنفيذي.
- `guides/Workflow_First_UI_Recovery_Plan.md`
  - الخطة الرسمية لإعادة بناء العلاقة بين الواجهة والعمل، بحيث تخدم الواجهة المسارات الحقيقية للبرنامج.
- `guides/Core_Workflows.md`
  - تعريف العمل الأساسي للبرنامج على مستوى المهام، لا على مستوى أسماء الشاشات.
- `guides/UI_Operational_Rules.md`
  - قواعد تشغيلية تحكم ما يظهر وما يهدأ وما يندمج داخل الواجهات.
- `guides/Surface_Audit_Matrix.md`
  - audit تنفيذي للسطوح الحالية، يبدأ فعليًا من `Requests` ويحدد ما يجب أن يبقى أو يهدأ أو يُعاد تنظيمه.
- `guides/UI_Recovery_Priority_Map.md`
  - الترتيب الشامل الأحدث لأولويات جميع الواجهات، مع سبب الأولوية ونوع التدخل المطلوب لكل واحدة.
- `guides/Navigation_Architecture_Map.md`
  - الخريطة المقترحة لبنية التنقل العليا: ما الذي يبقى top-level، وما الذي يندمج تحت عائلة أكبر.
- `guides/Canonical_Home_Matrix.md`
  - المرجع الرسمي لبيوت الوظائف: أين البيت الرسمي لكل مسار، وأين يسمح بظهوره ثانويًا فقط.
- `guides/UI_System_Map.md`
  - الخريطة الشاملة للبرنامج من مستوى العائلات والسايدبار إلى مستوى البيوت الرسمية وعلاقات السطوح ببعضها.
- `guides/UI_Component_Atlas.md`
  - أطلس تفصيلي لكل surface وما بداخلها من toolbars وكروت وأقسام وأزرار ونوافذ ثانوية ونوافذ نظامية.
- `guides/UIAutomation_Tooling_Roadmap.md`
  - خارطة الطريق الرسمية لتطوير أداة الفحص والتشغيل البصري/السلوكي نفسها، بما في ذلك إعادة الهيكلة التدريجية والاختبارات الرجعية.
- `guides/UIAutomation_Adaptive_Capabilities_Roadmap.md`
  - خارطة الطريق الخاصة بجعل الأداة تستدعي الفيديو/الصوت/الماوس والقدرات الثقيلة **لحظيًا** داخل نفس الاستكشاف الحر، مع بقاء الـ core خفيفًا افتراضيًا.
- `guides/UIAutomation_Supported_API.md`
  - العقد الرسمي الحالي للواجهة العامة المعتمدة لأداة UI Automation: ما الذي نعدّه API عامة، وما الذي يبقى داخليًا.
- `archive/README.md`
  - فهرس الأرشيف وتصنيف كل ملف: ما هو حي، وما هو تاريخي فقط.

## ما الذي يعتبر أرشيفًا

- `archive/`
  - مراجع تخطيطية وUX وواجهات قديمة، مع فهرس واضح لما بقي مفيدًا منها.
- `archive/legacy_ui/`
  - مواد قديمة مرتبطة بالواجهة السابقة.
- `../archive/v1_views/`
  - الشجرة القديمة الكاملة للواجهات، للرجوع المرجعي فقط.

## ما الذي لا يعد مرجعًا رسميًا

- أي ملف داخل `Assets/Documentation/Screenshots/UIAcceptance/latest/`
- أي مجلد تشغيل زمني مباشر تحت `Assets/Documentation/Screenshots/UIAcceptance/20*`

هذه نواتج generated للتحقق والتجربة، وليست baseline معتمدة.
