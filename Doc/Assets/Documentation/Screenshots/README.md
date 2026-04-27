# Screenshot Artifacts

هذا المجلد يجمع لقطات التحقق البصري المرتبطة بالواجهة الجديدة.

## البنية

- `UIAcceptance/`
  - نواتج التشغيل الآلي لاختبارات القبول البصري
  - كل تشغيل ينشئ مجلدًا زمنيًا مستقلًا يحتوي:
    - لقطات الشاشات
    - لقطات النوافذ الفرعية
    - لقطات نوافذ التأكيد
    - `summary.md`
  - كما يتم تحديث مجلد ثابت باسم `latest` ليحمل آخر تشغيل ناجح
  - **المهم:** `latest/` وأي مجلد زمني مباشر تحت `UIAcceptance/20*` هي مخرجات generated وتشغيلية، وليست baseline رسمية متتبعة
  - كما نحفظ baselines مسماة تحت:
    - `UIAcceptance/baselines/`
    - المرجع الحالي المعتمد:
      [2026-04-26-stabilization](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-stabilization)
    - وجولة القبول على البيانات المولدة الثقيلة:
      [2026-04-26-heavy-seed-acceptance](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-heavy-seed-acceptance)
    - وتقرير الجولات المتوازية على البرنامج والأداة:
      [2026-04-26-parallel-improvement-rounds.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-parallel-improvement-rounds.md)
    - وأول دفعة UAT تشغيلي:
      [2026-04-26-uat-pass-01.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-uat-pass-01.md)
    - وثاني دفعة UAT مع تنفيذ العمليات حتى النهاية:
      [2026-04-26-uat-pass-02.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-uat-pass-02.md)
    - وثالث دفعة UAT التي أغلقت فجوة النوافذ الأصلية للطباعة وأثبتت المعالجة المحلية لغياب مستند رد البنك:
      [2026-04-26-uat-pass-03.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-uat-pass-03.md)
    - ورابع دفعة UAT على baseline `preview.2` التي شددت حسم نوافذ التأكيد وأعادت `NewGuaranteeDiscard` و`All` إلى النجاح:
      [2026-04-26-uat-pass-04.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-uat-pass-04.md)
    - وأول جولة أداء بعد إغلاق `preview.2 polish` السلوكي:
      [2026-04-27-performance-pass-01.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-performance-pass-01.md)
    - والجولة الثانية التي شددت الحكم على مسار popup داخل `Settings`:
      [2026-04-27-performance-pass-02.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-performance-pass-02.md)
    - وجولة UAT الأوسع على بيانات مولدة من داخل `Settings`، والتي كشفت وأغلقت خلل التحديد stale بعد `reseed`:
      [2026-04-27-heavy-seed-uat-pass-01.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-27-heavy-seed-uat-pass-01.md)

## تشغيل الأداة

من جذر المشروع:

```powershell
.\scripts\run_ui_acceptance.ps1 -Scenario All
```

ولفحص الأداة نفسها regression smoke:

```powershell
.\scripts\run_ui_tooling_regression.ps1
```

والمخرجات الحالية تُكتب إلى:

- `UIAcceptance/latest/tooling-unit-summary.md`
- `UIAcceptance/latest/tooling-regression-summary.md`
- `UIAcceptance/latest/tooling-integration-summary.md`

وتغطي unit suite الآن أيضًا:

- إنشاء `contact sheet`
- مقارنة صورتين صناعيًا
- إنتاج `diff image` موثوقة بعد فصل طبقة `Capture`

ولتشغيل التكاملات الأوسع أو كل suites معًا:

```powershell
.\scripts\run_ui_tooling_regression.ps1 -Suite Integration
.\scripts\run_ui_tooling_regression.ps1 -Suite Unit
.\scripts\run_ui_tooling_regression.ps1 -Suite All
```

وللاستكشاف العام غير المرتبط بسيناريو ثابت:

```powershell
.\scripts\ui_explore.ps1 -Action Probe
.\scripts\ui_explore.ps1 -Action Diagnostics
.\scripts\ui_explore.ps1 -Action State
.\scripts\ui_explore.ps1 -Action Compare -ReferencePath ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\baselines\guarantees.png"
```

السيناريوهات الحالية:

- `SmokeNavigation`
- `NewGuaranteeDiscard`
- `All`

## الهدف

هذه الأداة ليست مجرد التقاط صور ثابتة. هي عدة قبول تشغيلي تعتمد على UI Automation حتى:

- تفتح البرنامج أو ترتبط بالجلسة الحالية
- تتنقل بين الشاشات
- تتعامل مع النوافذ الفرعية
- ترصد نوافذ التأكيد
- تحفظ لقطات موثقة داخل مسار ثابت

والفكرة من `baseline` المسماة ليست التجميل، بل أن نملك نقطة مرجعية نستطيع الرجوع لها بعد أي تعديل كبير في:

- السلوك
- اللقطات
- الأداء الزمني
- النوافذ الفرعية والتأكيدات

## ما الذي يُتتبع في Git؟

- `UIAcceptance/baselines/`
  - نعم، لأنها المرجع الرسمي
- `UIAcceptance/latest/`
  - لا، لأنها generated وتتغير مع كل تشغيل
- مجلدات التشغيل الزمنية المباشرة `UIAcceptance/20*`
  - لا، لأنها مخرجات فحص مرحلية

## أوامر الاستكشاف العامة

الأداة العامة [ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1) تسمح لنا باستخدام التطبيق حسب الحاجة أثناء الفحص، وليس فقط عبر سيناريوهات ثابتة.

الوضع الافتراضي الآن هو `Probe` لأنه الأخف في التشغيل.

أمثلة سريعة:

```powershell
.\scripts\ui_explore.ps1 -Action Windows
.\scripts\ui_explore.ps1 -Action Sidebar -Label "الضمانات"
.\\scripts\\ui_explore.ps1 -Action Sidebar -AutomationId "Shell.Sidebar.Guarantees"
.\\scripts\\ui_explore.ps1 -Action WaitWindow -WindowAutomationId "Dialog.NewGuarantee"
.\\scripts\\ui_explore.ps1 -Action WaitWindowClosed -WindowAutomationId "Dialog.NewGuarantee"
.\scripts\ui_explore.ps1 -Action Elements -WindowTitle "إجراء جديد" -ControlType "ControlType.Edit"
.\\scripts\\ui_explore.ps1 -Action Elements -WindowAutomationId "Dialog.NewGuarantee" -AutomationId "Dialog.NewGuarantee.GuaranteeNoInput"
.\scripts\ui_explore.ps1 -Action SetField -WindowTitle "إجراء جديد" -Label "رقم الضمان" -Value "TEST-001"
.\\scripts\\ui_explore.ps1 -Action SetField -WindowAutomationId "Dialog.NewGuarantee" -AutomationId "Dialog.NewGuarantee.GuaranteeNoInput" -Value "TEST-001"
.\scripts\ui_explore.ps1 -Action DialogAction -WindowTitle "تأكيد الإغلاق" -Text "Yes"
.\\scripts\\ui_explore.ps1 -Action DialogAction
.\scripts\ui_explore.ps1 -Action Capture -WindowTitle "سجل الضمان"
.\scripts\ui_explore.ps1 -Action Probe
.\scripts\ui_explore.ps1 -Action Diagnostics -MaxResults 12
.\scripts\ui_explore.ps1 -Action Probe -IncludeCapture
.\scripts\ui_explore.ps1 -Action Compare -WindowAutomationId "Shell.MainWindow" -ReferencePath ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\baselines\shell-main.png"
.\\scripts\\ui_explore.ps1 -Action HostState
.\\scripts\\ui_explore.ps1 -Action CapabilityOn -CapabilityName BurstCapture -LeaseMilliseconds 3000 -Reason "suspected-flicker"
.\\scripts\\ui_explore.ps1 -Action CapabilityOff -CapabilityName BurstCapture -Reason "done"
```

الهدف منها أن تمنحنا:

- استكشاف شجرة الواجهة وقت الحاجة
- التنقل الحر بين الشاشات والنوافذ
- تعبئة الحقول والضغط على الأزرار
- التحقق البصري والوظيفي في نفس اللحظة
- قراءة حالة الـ Shell وأحدث أحداث UI من ملفات التشخيص نفسها

## المسارات السريعة

- `Probe`
  - أخف قراءة متاحة
  - تعرض حالة الـ Shell وأحدث الأحداث والنوافذ المفتوحة
  - تعرض أيضًا ملخص الأداء الزمني وآخر الخط الزمني التفاعلي
  - مناسبة للحكم السريع قبل أي تصوير
  - لا تلتقط صورة إلا إذا طلبنا ذلك صراحة عبر `-IncludeCapture`
  - تفضّل النافذة النشطة فعليًا عند وجود حوار أو رسالة فوق التطبيق
  - وعند وجود نافذة أصلية خارج عملية التطبيق لكنها مرتبطة به، تتحول اللقطة تلقائيًا إلى **Desktop capture** بدل الاقتصاص على نافذة WPF فقط
  - تستفيد من `AutomationId` مباشرة متى كان متاحًا بدل المسح الكامل للشجرة

- `Diagnostics`
  - نفس الروح لكن مع تفاصيل أكثر من سجل الأحداث

- `State`
  - تشمل لقطة شاشة للحالة الحالية
  - نستخدمها عندما نحتاج الربط بين المنطق والصورة في نفس المخرجات

- `Compare`
  - تلتقط الحالة الحالية عند الطلب فقط
  - تقارنها بصورة مرجعية
  - تحفظ فرقًا بصريًا واضحًا وتعيد نسبة الاختلاف

## ملفات المساندة

- `interactive-timeline.jsonl`
  - خط زمني خفيف لتفاعلات الأداة نفسها مع مدد التنفيذ

- `scripts/ui_human_calibration.json`
  - ملف ثابت ومتتبع
  - يحتوي ملاحظات بشرية ومعايير حكم تساعد الأداة على تقريب تقييمها من تقييم المستخدم

## ملاحظتان مهمتان

- يمكن استخدام `DialogAction` بلا عنوان نافذة عندما نريد التعامل مع نافذة التأكيد النشطة مباشرة.
- `DialogAction` لم تعد تعتبر "الضغط على الزر" نجاحًا بحد ذاته؛ هي الآن تنتظر جاهزية الزر، ثم تتحقق من إغلاق الحوار فعليًا، وتعيد المحاولة بأسلوب بديل إذا لزم.
- `WaitWindowClosed` صارت متاحة الآن عندما نريد assert صريحة على اختفاء نافذة داخلية أو خارجية بعد الفعل.
- يمكن استخدام `SetField` إما عبر `Label` أو مباشرة عبر `AutomationId` إذا كان الحقل معروف الهوية.
- `SetField` تدعم الآن أيضًا القيمة الفارغة عندما نريد تفريغ الحقل نفسه بدل الالتفاف عبر `SendKeys`.
- `Sidebar` يمكن توجيهها الآن إما عبر `Label` أو عبر `AutomationId` ثابتة، وهذا يفيد خصوصًا عندما تكون بيئة التنفيذ نفسها حساسة للترميز العربي.
- التصوير والـ contact sheet لم يعودا يحمّلان مكتبات الرسوم إلا عند الحاجة الفعلية لهما.
- الأداة لم تعد تسمح بالتنقل العام أو النقر على النافذة الرئيسية بينما توجد رسالة أو حوار مفتوح فوق التطبيق؛ ستطلب أولًا حسم الحوار عبر `DialogAction` أو استهدافه صراحة.
- `Windows` و`Probe` لا يكتفيان الآن بنوافذ عملية التطبيق فقط؛ بل يعرضان أيضًا النوافذ الأصلية المرتبطة به مثل `GuaranteeManager - Print` عندما تظهر.
- `HostState` تعطي snapshot خفيفة للجلسة التكيفية نفسها:
  - session path
  - observation path
  - capability definitions
  - active capabilities
  - recent adaptive observations
- `CapabilityOn` و`CapabilityOff` تسمحان الآن بتفعيل قدرات لحظية مثل `BurstCapture` داخل نفس الاستكشاف الحر، ثم إطفائها من غير الخروج من الجلسة.
