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
- `guides/UIAutomation_Tooling_Roadmap.md`
  - خارطة الطريق الرسمية لتطوير أداة الفحص والتشغيل البصري/السلوكي نفسها، بما في ذلك إعادة الهيكلة التدريجية والاختبارات الرجعية.
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
