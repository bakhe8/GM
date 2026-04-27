# UI Automation Tooling Roadmap

**تاريخ الإنشاء:** 2026-04-27  
**النطاق:** تطوير وإعادة هيكلة أداة الفحص والتشغيل البصري/السلوكي داخل المشروع  
**الحالة:** مرجع تنفيذي حي  
**النسخة المرجعية الحالية:** `v1.1.0-preview.2`

---

## حالة التنفيذ الحالية

- **Phase 0**: مثبتة عمليًا
  - baseline الحالية موثقة
  - مسارات الاستخدام الحرجة معروفة
  - قياسات الأداء الأخيرة محفوظة
- **Phase 1**: بدأت فعليًا
  - استُخرجت طبقة `Windows/Dialog` إلى:
    - [scripts/modules/UiAutomation.Windows.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Windows.ps1:1)
    - [scripts/modules/UiAutomation.Dialogs.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Dialogs.ps1:1)
  - وبقي [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) كـ facade متوافقة
  - والتحقق التشغيلي الحالي:
    - `NewGuaranteeDiscard` نجح
    - `All` نجحت على جلسة نظيفة

---

## لماذا هذه الخطة؟

الأداة لم تعد مجرد سكربت مساعد. هي الآن طبقة تشغيل وتشخيص نعتمد عليها في:

- UAT
- التقاط السلوك البصري
- مقارنة الأداء
- تفسير العلاقة بين الواجهة والمنطق
- اكتشاف مشاكل التطبيق نفسه

وهذا نجاح ممتاز، لكنه يعني أن الأداة نفسها أصبحت تستحق:

1. **تطويرًا مستمرًا**
2. **تنظيمًا معماريًا**
3. **اختبارات رجعية**
4. **توثيقًا واضحًا كمنتج داخلي**

الحكم الحالي:

- **لا نحتاج rewrite من الصفر**
- **نحتاج refactor منظمًا على دفعات**
- **ونحتاج تثبيت عقد واضح لقدرات الأداة وسلوكها**

---

## الصورة الحالية

الأداة اليوم ترتكز أساسًا على:

- [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1)
- [scripts/ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1)
- [scripts/run_ui_acceptance.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_acceptance.ps1:1)
- [scripts/ui_human_calibration.json](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_human_calibration.json:1)

### الوضع الحالي باختصار

- `UIAutomation.Acceptance.psm1`:
  - يحتوي Core UIA + Windows + Dialogs + Diagnostics + Performance + Capture
  - الملف كبير جدًا، ويحمل مسؤوليات متعددة
- `ui_explore.ps1`:
  - صار أكثر من مجرد entry point
  - فيه routing ومنطق قرار وطبقة سلوك عامة
- `run_ui_acceptance.ps1`:
  - شغّال جيدًا كسيناريوهات قبول
  - لكنه يعتمد على core يجب أن يصبح أوضح

### المخاطر الحالية

1. تضخم الملف الأساسي
2. اختلاط طبقات المسؤولية
3. صعوبة اختبار الأداة نفسها بشكل معزول
4. خطر كسر مسار قديم أثناء إصلاح مسار جديد
5. بطء فهم أي bug جديد داخل الأداة

---

## أهداف هذه الخارطة

### الهدف الرئيسي

بناء أداة:

- خفيفة في التشغيل
- واضحة البنية
- قابلة للتوسع
- قابلة للاختبار
- موثوقة في الحكم
- سهلة التطوير بدون كسر القدرات الحالية

### الأهداف التفصيلية

1. فصل المسؤوليات داخل الأداة
2. تثبيت API داخلية أوضح بين الأجزاء
3. جعل `ui_explore.ps1` طبقة توجيه خفيفة
4. إضافة regression checks للأداة نفسها
5. تحسين الأداء في المسارات الشائعة
6. تحسين التشخيص عند الفشل من أول لحظة
7. الحفاظ على التوافق مع الأوامر الحالية أثناء الانتقال

---

## ما الذي لن نفعله؟

هذه الخطة **لا** تهدف إلى:

- إعادة كتابة الأداة بلغة أخرى
- استبدال PowerShell الآن
- بناء إطار اختبار UI عام منفصل عن المشروع
- كسر أوامر الاستخدام الحالية ثم إصلاحها لاحقًا
- إضافة تعقيد معماري أكبر من الحاجة

---

## البنية المستهدفة

الهيكل المقترح داخل `scripts/`:

```text
scripts/
  modules/
    UiAutomation.Core.psm1
    UiAutomation.Windows.psm1
    UiAutomation.Actions.psm1
    UiAutomation.Dialogs.psm1
    UiAutomation.Diagnostics.psm1
    UiAutomation.Capture.psm1
    UiAutomation.Session.psm1
  tests/
    UiAutomation.Core.Tests.ps1
    UiAutomation.Windows.Tests.ps1
    UiAutomation.Dialogs.Tests.ps1
    UiAutomation.Diagnostics.Tests.ps1
    UiAutomation.Smoke.Tests.ps1
  UIAutomation.Acceptance.psm1
  ui_explore.ps1
  run_ui_acceptance.ps1
  ui_human_calibration.json
```

### 1. `UiAutomation.Core.psm1`

مسؤول عن:

- `Find`
- `Wait`
- `Invoke`
- `SetValue`
- `ControlType resolution`
- التعامل الخام مع عناصر UI Automation

أمثلة لما ينبغي نقله إليه:

- `Resolve-UiControlType`
- `Find-UiElementsFast`
- `Wait-UiElement`
- `Invoke-UiElement`
- `Set-UiElementValue`
- `Get-UiClickableAncestor`

### 2. `UiAutomation.Windows.psm1`

مسؤول عن:

- النوافذ العليا
- foreground/owner logic
- popup detection
- external windows
- window catalogs
- focus/show/restore

أمثلة:

- `Get-UiTopLevelWindows`
- `Get-UiForegroundWindowElement`
- `Get-UiRelatedTopLevelWindows`
- `Resolve-UiWindow`
- `Show-UiWindow`
- `Get-UiWindowsCatalog`

### 3. `UiAutomation.Actions.psm1`

مسؤول عن:

- الضغط
- المفاتيح
- `SendKeys`
- استهداف العنصر الفعلي
- توحيد result contract للأفعال

أمثلة:

- `Resolve-UiActionTarget`
- منطق `Click`
- منطق `Key`
- منطق `SendKeys`
- أي fallback logic متعلق بالتفاعل لا بالاكتشاف

### 4. `UiAutomation.Dialogs.psm1`

مسؤول عن:

- اكتشاف الحوارات
- أزرار التأكيد
- جاهزية أزرار الحوار
- انتظار اختفاء الحوار
- التعامل مع message boxes والحوارات الثانوية

أمثلة:

- `Get-UiActiveDialog`
- `Get-UiDialogActionButton`
- `Wait-UiElementReady`
- `Wait-UiWindowGone`
- `Invoke-UiDialogActionButton`

### 5. `UiAutomation.Diagnostics.psm1`

مسؤول عن:

- timeline
- event logs
- shell state
- performance summary
- assessment
- calibration

أمثلة:

- `Get-UiDiagnosticsPaths`
- `Write-UiTimelineEvent`
- `Get-UiTimelineEntries`
- `Get-UiPerformanceSummary`
- `Get-UiShellStateSnapshot`
- `Get-UiRecentEvents`
- `Get-UiCalibrationProfile`

### 6. `UiAutomation.Capture.psm1`

مسؤول عن:

- window capture
- desktop capture
- compare/diff
- contact sheet generation

ويستوعب أي منطق مرئي يجب عزله عن منطق التفاعل.

### 7. `UiAutomation.Session.psm1`

مسؤول عن:

- تشغيل التطبيق
- الارتباط بجلسة قائمة
- اكتشاف العملية الصحيحة
- تنظيف الحالات الانتقالية
- policy الخاصة بإعادة استخدام الجلسة

أمثلة:

- `Start-UiTargetApplication`
- `Get-UiProcess`
- أي helper متعلق بالجلسة لا بالعناصر

### 8. `UIAutomation.Acceptance.psm1`

لن يبقى ملف “كل شيء”.

دوره المستهدف:

- facade / compatibility layer
- يعيد export الدوال العامة
- يحافظ على التوافق مع الأوامر الحالية
- ويُخفي البنية الجديدة خلف API مستقرة

---

## المبادئ المعمارية التي سنلتزم بها

1. **Backward compatibility أولًا**
   - لا نكسر أوامر:
   - [ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1)
   - [run_ui_acceptance.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_acceptance.ps1:1)

2. **Facade ثابتة**
   - `UIAutomation.Acceptance.psm1` يبقى هو المدخل الرسمي مؤقتًا

3. **نتيجة موحدة لكل فعل**
   - Contract موحد للأفعال

4. **القياس جزء من التنفيذ**
   - لا يوجد فعل مهم بدون tracing واضح

5. **الفشل يجب أن يكون قابلًا للتفسير**
   - ماذا فشل؟
   - أين؟
   - على أي نافذة؟
   - بعد كم ms؟
   - وما artifact الذي يوثقه؟

6. **الصورة المبكرة عند الفشل**
   - عند الفشل المهم، الأداة تحفظ الدليل البصري تلقائيًا حيث يلزم

---

## العقد المستهدفة للنتائج

نحتاج contract موحدة للعائد من الأفعال الأساسية، مثل:

```text
ActionResult
  Action
  Success
  DurationMs
  ProcessId
  Window
  Target
  Strategy
  Warnings[]
  Artifacts[]
  Diagnostics
```

والمقصود ليس بالضرورة كلاس رسمي، بل بنية ثابتة ومتوقعة.

هذا مهم في:

- `Click`
- `DialogAction`
- `SetField`
- `Key`
- `Capture`
- `Compare`

---

## خارطة التنفيذ — على مراحل

## المرحلة 0 — Freeze وتعريف خط الأساس

### الهدف
بدء إعادة الهيكلة من وضع مفهوم ومقاس.

### العمل

1. تثبيت baseline الحالية:
   - `preview.2`
   - آخر جولات الأداء
2. تسجيل:
   - عدد الدوال الحالية
   - حجم الملفات
   - أهم المسارات الحرجة
3. تحديد:
   - الأوامر العامة التي يجب ألا تنكسر

### المخرجات

- baseline موثقة
- قائمة أوامر حرجة
- قائمة public functions الحالية

### بوابة النجاح

- نعرف بالضبط ما الذي يجب أن يبقى شغالًا بعد كل مرحلة

---

## المرحلة 1 — استخلاص طبقة Windows/Dialog

### لماذا نبدأ هنا؟

لأن أكثر ما تعقده الأداة اليوم هو:

- popup
- message boxes
- external windows
- dialogs

وهذا أكثر جزء يتكرر في الأعطال والتحسينات.

### العمل

1. إنشاء:
   - `UiAutomation.Windows.psm1`
   - `UiAutomation.Dialogs.psm1`
2. نقل الدوال المتعلقة بالنوافذ والحوارات
3. إبقاء `UIAutomation.Acceptance.psm1` يعيد exportها فقط
4. إضافة smoke checks بعد النقل

### أمثلة النقل

- `Resolve-UiWindow`
- `Get-UiWindowsCatalog`
- `Get-UiActiveDialog`
- `Invoke-UiDialogActionButton`
- `Wait-UiWindowGone`

### المخاطر

- كسر detection للحوار النشط
- كسر النوافذ الخارجية مثل الطباعة

### بوابة النجاح

- `DialogAction`
- `WaitWindow`
- `Probe`
- `Windows`

كلها تعمل كما كانت قبل النقل

---

## المرحلة 2 — استخلاص طبقة Core UIA

### الهدف

عزل منطق العناصر الخام عن منطق الاستخدام.

### العمل

1. إنشاء `UiAutomation.Core.psm1`
2. نقل:
   - `Find`
   - `Wait`
   - `Invoke`
   - `SetValue`
3. حذف التكرار بين:
   - `Find-UiElements`
   - `Find-UiProcessElements`
   - `Find-UiElementsFast`
4. توحيد naming ونمط الإدخال

### المخاطر

- بطء غير مقصود بعد النقل
- كسر fallback logic القديم

### بوابة النجاح

- `Elements`
- `SetField`
- `Click`
- `Key`

تبقى سليمة على نفس الحوارات والمساحات الحالية

---

## المرحلة 3 — تخفيف `ui_explore.ps1`

### الهدف

تحويله من ملف شبه orchestration كبير إلى:

- parser للمدخلات
- router للأفعال
- formatter للنتائج

### العمل

1. إبقاء:
   - parameter parsing
   - action dispatch
   - JSON output shaping
2. نقل logic الثقيل إلى modules
3. تقليص `Invoke-UiAction`
4. تقسيم المساعدات المحلية إلى:
   - session helpers
   - payload builders
   - dispatch

### بوابة النجاح

- الملف ينخفض بوضوح
- فهم المسار من `Action` إلى التنفيذ يصبح مباشرًا

---

## المرحلة 4 — طبقة Diagnostics وPerformance

### الهدف

فصل التشخيص عن التفاعل.

### العمل

1. إنشاء `UiAutomation.Diagnostics.psm1`
2. نقل:
   - timeline
   - calibration
   - shell state
   - performance summaries
   - assessment
3. توحيد policy الخاصة بالقياسات
4. فصل ما هو:
   - raw measurement
   - human-calibrated judgment

### بوابة النجاح

- `Probe`
- `Diagnostics`
- `State`

تستمر في إعطاء نفس المخرجات تقريبًا، لكن من بنية أوضح

---

## المرحلة 5 — طبقة Capture & Visual Evidence

### الهدف

أن يصبح التصوير مكونًا مستقلاً، ويُستخدم بذكاء عند الحاجة.

### العمل

1. إنشاء `UiAutomation.Capture.psm1`
2. نقل:
   - capture
   - compare
   - image loading
   - desktop/window fallback
3. إضافة policy:
   - capture on failure
   - capture on external foreground window
   - preserve latest vs baseline rules

### بوابة النجاح

- `Capture`
- `Compare`
- `Probe -IncludeCapture`

كلها تعمل بنفس السلوك الحالي أو أفضل

---

## المرحلة 6 — اختبار الأداة نفسها

### الهدف

تحويل الخبرة المتراكمة إلى regression suite.

### المقترح

إضافة `scripts/tests/` مع Pester tests أو smoke tests منظمة.

### المستويات

#### A. اختبارات وحدة خفيفة

لأجزاء مثل:

- matching logic
- thresholds
- payload shaping

#### B. اختبارات تكامل صغيرة

مثل:

- active dialog resolution
- popup menu resolution
- external window detection
- window closed waiting

#### C. اختبارات smoke تشغيلية

مثل:

- launch
- sidebar
- new guarantee discard
- settings tools popup

### بوابة النجاح

- نستطيع تغيير الأداة بثقة أعلى
- نعرف بسرعة إذا كسرنا behavior سابقًا

---

## المرحلة 7 — تثبيت API واستخدامها داخليًا

### الهدف

إعلان الأداة كطبقة مستقرة نسبيًا.

### العمل

1. توثيق الدوال العامة المعتمدة
2. تقليل الدوال “المكشوفة بالصدفة”
3. تمييز:
   - public API
   - internal helpers
4. توثيق patterns الرسمية للاستعمال

### بوابة النجاح

- كل من يقرأ الأداة يعرف:
  - ما الذي يجوز استدعاؤه
  - وما الذي هو internal

---

## ترتيب التنفيذ المقترح

### الدفعة 1

1. مرحلة 0
2. مرحلة 1

### الدفعة 2

3. مرحلة 2
4. مرحلة 3

### الدفعة 3

5. مرحلة 4
6. مرحلة 5

### الدفعة 4

7. مرحلة 6
8. مرحلة 7

هذا الترتيب متعمد:

- نبدأ من أكثر المناطق ألمًا
- ثم نفصل core
- ثم نخفف الواجهة
- ثم نثبت التشخيص والتصوير
- ثم نضيف الاختبارات

---

## خطة commits المقترحة

بدل دفعة ضخمة واحدة:

1. `refactor: extract window and dialog automation modules`
2. `refactor: extract core UI automation primitives`
3. `refactor: slim ui_explore action dispatch`
4. `refactor: extract diagnostics and performance helpers`
5. `refactor: extract capture and compare helpers`
6. `test: add UI tooling regression checks`
7. `docs: document UI tooling architecture and supported API`

---

## معايير النجاح النهائية

سنعتبر الخارطة ناجحة إذا وصلنا إلى:

1. `UIAutomation.Acceptance.psm1` صار facade أصغر بوضوح
2. `ui_explore.ps1` صار router أنظف
3. popup/dialog/external windows لم تعد منطقًا مبعثرًا
4. لدينا regression checks للأداة نفسها
5. الأداء لم يتراجع بعد التفكيك
6. كل الجولات الحالية (`Probe`, `DialogAction`, `Click`, `Compare`, `All`) بقيت سليمة

---

## المخاطر وكيف نحتويها

### 1. كسر التوافق

**الاحتواء:** facade ثابتة + smoke checks بعد كل دفعة

### 2. تحسن معماري مع تراجع تشغيلي

**الاحتواء:** القياس قبل/بعد في كل مرحلة

### 3. تضخم plan أكثر من التنفيذ

**الاحتواء:** كل مرحلة يجب أن تنتهي بـ commit فعلي واختبار حي

### 4. التوسع في abstraction بلا حاجة

**الاحتواء:** reuse حقيقي فقط، لا تقسيم شكلي

---

## ما الذي سنراقبه أثناء التنفيذ؟

### مؤشرات صحية

- زمن `Sidebar`
- زمن popup/menu resolution
- موثوقية `DialogAction`
- عدد false failures
- وضوح `Probe`
- حجم الملفات
- سهولة إصلاح bug جديد

### مؤشرات خطر

- ارتفاع عدد fallback hacks
- تضاعف المنطق نفسه في أكثر من مكان
- الحاجة إلى فتح نفس الملف الكبير في كل bug
- عدم القدرة على معرفة هل الخلل من الأداة أم من التطبيق

---

## القرار التنفيذي

هذه الخارطة ليست “اقتراحًا جميلًا” فقط.  
هي الآن **أفضل استثمار تقني قريب المدى** بعد أن أصبحت الأداة جزءًا من طريقة تطويرنا نفسها.

### الأولوية المقترحة الآن

1. **استخلاص Windows/Dialog**
2. **ثم Core**
3. **ثم تخفيف `ui_explore.ps1`**

إذا أُنجزت هذه الثلاثة فقط جيدًا، سنشعر بالفرق مباشرة في:

- سرعة التطوير
- ثقة UAT
- سهولة التشخيص
- وإيقاع الإصلاح

---

## المرفقات المرجعية

- [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1)
- [scripts/ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1)
- [scripts/run_ui_acceptance.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_acceptance.ps1:1)
- [scripts/ui_human_calibration.json](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_human_calibration.json:1)
- [Doc/CURRENT_STATE.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md:1)
- [Doc/Assets/Documentation/Screenshots/README.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/README.md:1)
