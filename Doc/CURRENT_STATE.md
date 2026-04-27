# الحالة الحالية المختصرة — GuaranteeManager

**تاريخ التحديث:** 2026-04-27  
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
- وبدأت الآن أول جولة أداء حقيقية بعد إغلاق `preview.2 polish` السلوكي:
  - `Sidebar` تحسنت من حوالي `2072ms` إلى `1146ms` في الجولة الأوسع
  - `Click` داخل الحوارات الثقيلة نزلت إلى نطاق `~400–600ms`
  - `Launch` صار يُقاس بعتبة خاصة به بدل أن يظهر تحذيرًا كاذبًا تحت العتبة العامة
- ثم أُنجزت جولة UAT أوسع على بيانات مولدة من داخل `Settings`، وكشفت خللًا حقيقيًا بعد `reseed`:
  - `Shell` كانت تحتفظ بضمان محدد stale بعد إعادة بناء قاعدة البيانات
  - النتيجة كانت رسالة `تعذر العثور على الضمان المحدد` بدل فتح `OperationalInquiryDialog`
  - أُغلق هذا بإضافة refresh مركزي بعد `seed-development-data`
  - وأُعيد التحقق حيًا: `ShellState.Reason = data-reset` ثم عاد `OperationalInquiryDialog` ليفتح بنجاح
- ثم أُنجزت `Performance Pass 02` على مسار `Settings -> أدوات -> توليد بيانات تجريبية`:
  - تحسن استهداف popup/menu items داخل الأداة عبر fallback process-wide أسرع
  - زر `أدوات` هبط إلى `866.69ms` وصار ضمن الحد المقبول
  - عنصر `توليد بيانات تجريبية` هبط إلى `1711.18ms` بدل القياسات السابقة التي وصلت إلى `~4–6s`
  - والحكم الآن أوضح: bottleneck المتبقي هنا أقرب إلى إظهار نافذة التأكيد داخل التطبيق نفسه، لا إلى بحث الأداة عن عنصر القائمة

## الأولوية الحالية

بعد إقفال جولة stabilization الأساسية ورفع الشجرة إلى `preview.2`، صارت الأولوية الحالية هي:

1. **Controlled UAT**
2. **Real-data validation**
3. **Performance and usability polish**

وبشكل موازٍ، صار لدينا الآن مسار هندسي واضح لتطوير الأداة نفسها بدل تركها تكبر بطريقة عضوية فقط:

- [guides/UIAutomation_Tooling_Roadmap.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Tooling_Roadmap.md:1)

وأول خطوة تنفيذية منه بدأت فعليًا الآن:

- استخراج طبقة `Windows/Dialog` إلى:
  - [scripts/modules/UiAutomation.Windows.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Windows.ps1:1)
  - [scripts/modules/UiAutomation.Dialogs.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Dialogs.ps1:1)
- مع إبقاء [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) كـ facade متوافقة
- والتحقق التشغيلي الحالي على هذا الاستخلاص نجح في:
  - `NewGuaranteeDiscard`
  - `All` على جلسة نظيفة
- ثم أُنجزت **Phase 2** من نفس الخارطة:
  - استُخرجت طبقة `Core UIA` إلى:
    - [scripts/modules/UiAutomation.Core.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Core.ps1:1)
  - وصار [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) يحمّل الآن:
    - `UiAutomation.Core.ps1`
    - `UiAutomation.Windows.ps1`
    - `UiAutomation.Dialogs.ps1`
  - والتحقق التشغيلي بعد هذا الفصل نجح في:
    - `Probe`
    - `All` على جلسة نظيفة
- ثم أُنجزت **Phase 3** من نفس الخارطة:
  - صار [scripts/ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1) طبقة routing وتتبع خفيفة فقط
  - واستُخرج منطقها إلى:
    - [scripts/modules/UiAutomation.Session.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Session.ps1:1)
    - [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
    - [scripts/modules/UiAutomation.Actions.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Actions.ps1:1)
  - حجم `ui_explore.ps1` هبط من نحو `597` سطرًا إلى نحو `98` سطرًا
  - والتحقق التشغيلي بعد هذا الفصل نجح في:
    - `Probe`
    - `Sidebar`
    - `Click`
    - `SetField`
    - `DialogAction`
- ثم أُنجزت **Phase 4** من نفس الخارطة:
  - انتقل قلب التشخيص نفسه من [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) إلى:
    - [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
  - وهذا يشمل الآن:
    - diagnostics paths
    - calibration profile
    - timeline writes/reads
    - performance summaries
    - shell state snapshot
    - recent events parsing
  - والتحقق التشغيلي بعد هذا الفصل نجح في:
    - `Probe`
    - `Sidebar`
    - `All` على جلسة نظيفة
- ثم اتسعت **Phase 6** عمليًا من smoke أولية إلى regression tooling أوضح:
  - أضفنا:
    - [scripts/tests/UiAutomation.Tooling.Smoke.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Smoke.ps1:1)
    - [scripts/tests/UiAutomation.Tooling.Integration.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Integration.ps1:1)
    - [scripts/run_ui_tooling_regression.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_tooling_regression.ps1:1)
  - كما أضيفت action عامة جديدة داخل الأداة:
    - `WaitWindowClosed`
  - والجولة الحالية تتحقق فعليًا من:
    - `Probe`
    - `Sidebar`
    - `Elements`
    - `WaitWindow`
    - `WaitWindowClosed`
    - `SetField`
    - `DialogAction`
    - popup/menu resolution داخل `Settings`
    - external window detection عبر `GuaranteeManager - Print`
    - والرجوع إلى جلسة نظيفة بعد إغلاق الحوارات الداخلية والخارجية
  - والنتيجة الحالية:
    - `10/10` في smoke
    - `14/14` في integration
    - وتشغيل `-Suite All` يمر الآن بنجاح
    - والملخصان الحيان يُكتبان هنا:
      - [UIAcceptance/latest/tooling-regression-summary.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/tooling-regression-summary.md)
      - [UIAcceptance/latest/tooling-integration-summary.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/tooling-integration-summary.md)
- ثم بدأت **Phase 7** عمليًا أيضًا:
  - صار للأداة عقد API حي داخل الكود نفسه عبر:
    - [Get-UiSupportedApi](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1)
  - وأضفنا suite خفيفة مستقلة للأجزاء الثابتة:
    - [scripts/tests/UiAutomation.Tooling.Unit.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Unit.ps1:1)
  - والجولة الحالية تتحقق من:
    - فئات الـ API العامة
    - تطابق `Export-ModuleMember` مع العقد الرسمي
    - شكل مسارات التشخيص
    - ملف المعايرة
    - قراءة timeline
    - شكل performance summary
  - والنتيجة الحالية:
    - `8/8` في unit
  - كما صار runner يدعم:
    - `Smoke`
    - `Integration`
    - `Unit`
    - `All`
  - والمرجع التوثيقي لهذه الطبقة هنا:
    - [guides/UIAutomation_Supported_API.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Supported_API.md:1)
    - [UIAcceptance/latest/tooling-unit-summary.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/tooling-unit-summary.md)

كما أضيف الآن مرجع حي لمراجعة نضج السطوح الأساسية الأربع مقابل التوجيه المستخرج من الأرشيف:

- [guides/Focused_Surface_Review_From_Archive.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Focused_Surface_Review_From_Archive.md:1)
- [guides/Preview2_Polish_Backlog.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Preview2_Polish_Backlog.md:1)

كما أُنجزت الآن الجولات الأولى من polish على:

- `لوحة التحكم`
- `ملف الضمان`
- `الطلبات`
- والجزء المتكرر من `الاكتمال الصامت` داخل `الطلبات` و`الضمانات`
- ثم تأكدنا بصريًا من هذا المسار داخل `الطلبات`، وأغلقنا فجوة صغيرة في `HistoryDialog` بحيث صار `نسخ رقم الضمان` يعلن النجاح عبر الشريط السفلي بدل النسخ الصامت الكامل
- ثم أُجريت مراجعة بصرية على `لوحة التحكم` و`ملف الضمان`، وأُغلق فرق صغير في لوحة التحكم بحيث صارت أزرار الصف تسمي المساحة المقصودة بدل زر `انتقال` العام
- ثم أُغلقت فجوة تشغيلية صغيرة في `RequestsDialog` داخل ملف الضمان: الأزرار الثقيلة أصبحت تتبع صلاحية الطلب المحدد بدل أن تبدو متاحة دائمًا
- ثم أُغلقت فجوة نضج صغيرة في `OperationalInquiryDialog`: الأزرار المعطلة صارت تشرح سبب التعطيل عبر tooltips واضحة بدل أن تبقى صامتة
- ثم أُنجزت مراجعة `OperationalInquiryDialog` من زاوية `الجواب ثم الفعل` وثُبتت حيًا:
  - أُضيفت بطاقة `الخطوة التالية` داخل لوحة السياق اليمنى
  - تختار الفعل الأنسب حسب الحالة الفعلية:
    - `فتح رد البنك`
    - أو `فتح خطاب الطلب`
    - أو `فتح مرفقات الإصدار`
    - أو `فتح السجل`
    - أو `تقرير الضمان`
  - وفي التحقق الحي الحالي اقترحت `فتح السجل`، ثم نجح الزر فعلًا وفتح `HistoryDialog` مباشرة
  - اللقطة المرجعية الحالية هنا:
    - [UIAcceptance/latest/operational-inquiry-next-step-review.png](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/operational-inquiry-next-step-review.png)
- ثم أُغلقت فجوة خروج صغيرة في `RequestsDialog`: صار للحوار زر `إغلاق` واضح، وأصبح `Escape` يغلقه كما يتوقع المستخدم، مع تحسين بسيط للأداة حتى لا تعتبر اختفاء الحوار السريع فشلًا
- ثم طُبقت أول سياسة واضحة لـ `Settings`: نجاحات الإنشاء والنسخ والتوليد التجريبي صارت هادئة عبر شريط الحالة، بينما مسارات الاسترجاع بقيت modal لأنها تحمل أثرًا أعلى وتفاصيل أمان مهمة
- ثم ثُبت هذا حيًا على مسار `نسخ ملخص المسارات` من `Settings`: لا تظهر نافذة نجاح modal، ويظهر بدلها نص نجاح واضح في الشريط السفلي، مع لقطة مرجعية هنا:
  - [UIAcceptance/latest/settings-copy-status.png](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/settings-copy-status.png)
- ثم أُنجزت `معاينة الأثر` داخل `NewGuaranteeDialog` و`EditGuaranteeDialog` وثُبتت حيًا:
  - `إجراء جديد` يبدأ بحالة منع واضحة، ثم ينتقل إلى `سيُنشأ ضمان جديد كإصدار أول (v1)` عند اكتمال الحد الأدنى من البيانات
  - `تعديل الضمان` يبدأ بحالة `لا توجد تغييرات جديدة`، ثم ينتقل إلى `سيُنشأ إصدار جديد v2` عند تعديل حقل واحد
- كما خرجت الأداة نفسها بتحسين مباشر من هذه الجولة:
  - `SetField` صار يدعم الآن عناصر `ComboBox` القابلة للتحرير عبر `ValuePattern` بدل الاعتماد القسري على `SendKeys`، لأن المسار القديم أمكنه تلويث الحقل التالي وفتح حفظ افتراضي على بعض الحوارات
- ثم أُنجزت مراجعة `HistoryDialog` من زاوية "الخطوة التالية" وثُبتت حيًا:
  - ظهرت بطاقة سياقية واضحة في الثلث العلوي من لوحة التفاصيل
  - وتغيرت توصيتها حسب التبويب والحالة:
    - `فتح مرفقات الإصدار` أو `تصدير السجل` في الإصدارات
    - `فتح خطاب الطلب` أو `فتح رد البنك` أو `عرض الإصدار الناتج/الأساس` في الطلبات
  - كما صارت قيم `الإصدار الأساس/الناتج` تعرض `VersionLabel` البشري بدل القيم الداخلية الخام

والحصر الحالي يقول إن المتبقي الحقيقي من `preview.2 polish` يتمحور الآن في:

- استكمال جولة الأداء بعد UAT أوسع وعلى بيانات أثقل أو حقيقية/معقمة
- ومراقبة ما إذا كان ما بقي bottleneck في الأداة أو في التطبيق نفسه

والحكم الحالي صار أوضح:

- البطء الأثقل المتبقي في هذا المسار يميل إلى **التطبيق نفسه**:
  - القوائم المنبثقة
  - نوافذ التأكيد
  - والعمليات المدمرة مثل `seed-development-data`
- أما كلفة الأداة المتبقية فتركز أكثر في:
  - اكتشاف عناصر popup/menu عبر UI Automation
  - وهي أقل حدة من bottleneck التطبيق نفسه في هذه الجولة

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
- كما توجد أول جولة أداء موثقة هنا:
  - [UIAcceptance/baselines/2026-04-27-performance-pass-01.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-performance-pass-01.md)
- كما توجد الجولة الثانية التي شددت الحكم على مسار popup داخل `Settings`:
  - [UIAcceptance/baselines/2026-04-27-performance-pass-02.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-performance-pass-02.md)
- كما توجد جولة UAT أوسع على بيانات مولدة من داخل `Settings` هنا:
  - [UIAcceptance/baselines/2026-04-27-heavy-seed-uat-pass-01.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-heavy-seed-uat-pass-01.md)
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
