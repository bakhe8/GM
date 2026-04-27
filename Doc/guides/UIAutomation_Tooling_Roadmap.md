# UI Automation Tooling Roadmap

**تاريخ الإنشاء:** 2026-04-27  
**النطاق:** تطوير وإعادة هيكلة أداة الفحص والتشغيل البصري/السلوكي داخل المشروع  
**الحالة:** مرجع تنفيذي حي  
**النسخة المرجعية الحالية:** `v1.1.0-preview.2`

---

## حالة التنفيذ الحالية

> **ملاحظة تنظيمية:** هذه الخارطة تعالج إعادة الهيكلة العامة للأداة نفسها.  
> أما طبقة **adaptive capabilities** التي تضيف session host + capability broker + on-demand providers فوق هذا الأساس، فمرجعها المخصص هنا:
>
> - [UIAutomation_Adaptive_Capabilities_Roadmap.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Adaptive_Capabilities_Roadmap.md:1)

- **Phase 0**: مثبتة عمليًا
  - baseline الحالية موثقة
  - مسارات الاستخدام الحرجة معروفة
  - قياسات الأداء الأخيرة محفوظة
- **Phase 1**: مكتملة
  - استُخرجت طبقة `Windows/Dialog` إلى:
    - [scripts/modules/UiAutomation.Windows.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Windows.ps1:1)
    - [scripts/modules/UiAutomation.Dialogs.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Dialogs.ps1:1)
  - وبقي [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) كـ facade متوافقة
  - والتحقق التشغيلي الحالي:
    - `NewGuaranteeDiscard` نجح
    - `All` نجحت على جلسة نظيفة
- **Phase 2**: مكتملة
  - استُخرجت طبقة `Core UIA` إلى:
    - [scripts/modules/UiAutomation.Core.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Core.ps1:1)
  - وصار [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) يحمّل الآن:
    - `UiAutomation.Core.ps1`
    - `UiAutomation.Windows.ps1`
    - `UiAutomation.Dialogs.ps1`
  - والتحقق التشغيلي الحالي:
    - `Probe` نجحت
    - `All` نجحت على جلسة نظيفة
- **Phase 3**: مكتملة
  - صار [scripts/ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1) طبقة routing وتتبع خفيفة
  - واستُخرجت المساعدات إلى:
    - [scripts/modules/UiAutomation.Session.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Session.ps1:1)
    - [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
    - [scripts/modules/UiAutomation.Actions.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Actions.ps1:1)
  - حجم `ui_explore.ps1` انخفض من نحو `597` سطرًا إلى نحو `98`
  - والتحقق التشغيلي الحالي:
    - `Probe`
    - `Sidebar`
    - `Click`
    - `SetField`
    - `DialogAction`
- **Phase 4**: مكتملة
  - انتقل قلب التشخيص نفسه إلى:
    - [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
  - وصار [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) يحمّل الآن:
    - `UiAutomation.Diagnostics.ps1`
    - `UiAutomation.Core.ps1`
    - `UiAutomation.Windows.ps1`
    - `UiAutomation.Dialogs.ps1`
  - والتحقق التشغيلي الحالي:
    - `Probe`
    - `Sidebar`
    - `All` على جلسة نظيفة
- **Phase 5**: مكتملة
  - استُخرجت طبقة `Capture & Visual Evidence` إلى:
    - [scripts/modules/UiAutomation.Capture.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Capture.ps1:1)
  - وصار [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) يحمّل الآن:
    - `UiAutomation.Core.ps1`
    - `UiAutomation.Windows.ps1`
    - `UiAutomation.Dialogs.ps1`
    - `UiAutomation.Capture.ps1`
    - `UiAutomation.Diagnostics.ps1`
  - كما اتسعت unit regression لتغطي:
    - `New-UiContactSheet`
    - `Compare-UiImages`
    - وإنتاج diff image بشكل موثوق
  - ومع تقدم adaptive roadmap صار لدينا الآن أيضًا طبقة وسائط مستقلة:
    - [scripts/modules/UiAutomation.Media.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Media.ps1:1)
  - وهذه الطبقة لا تثقل الـ core دائمًا، بل تضيف:
    - provider catalog واضح
    - media session قابلة للقراءة
    - single-instance handling لمزود الفيديو الحالي
    - وعقد scope attestation يوضح صراحة:
      - ما العملية والنافذة المستهدفتان
      - وهل foreground المرتبطة بالالتقاط بقيت ضمن البرنامج أم لا

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

**ملاحظة مبدئية مهمة:**  
هذه الأداة ليست منتجًا موجّهًا للبشر أولًا؛ بل هي **طبقة تضخيم لقدرات النموذج الذكي نفسه**.  
لذلك سنحاكم أي قرار معماري فيها بالسؤال التالي:

- هل يزيد حرية الاستكشاف؟
- هل يزيل الاعتماد على مسار واحد؟
- هل يسمح بتبديل الوسيلة داخل نفس الجلسة؟
- هل يبقي الأداة خفيفة حتى عندما نزيد قدراتها؟

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
- وتخدم **الاستكشاف الحر غير المقيد للنموذج** بدل أن تحبسه داخل سيناريوهات أو مراحل ثابتة

### الأهداف التفصيلية

1. فصل المسؤوليات داخل الأداة
2. تثبيت API داخلية أوضح بين الأجزاء
3. جعل `ui_explore.ps1` طبقة توجيه خفيفة
4. إضافة regression checks للأداة نفسها
5. تحسين الأداء في المسارات الشائعة
6. تحسين التشخيص عند الفشل من أول لحظة
7. الحفاظ على التوافق مع الأوامر الحالية أثناء الانتقال
8. التأكد أن الوصول إلى النتيجة لا يعتمد على modality واحدة أو selector واحد أو path واحد

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
    UiAutomation.Freedom.Tests.ps1
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

**الحالة:** مكتملة

### الهدف

عزل منطق العناصر الخام عن منطق الاستخدام.

### العمل

1. إنشاء [scripts/modules/UiAutomation.Core.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Core.ps1:1)
2. نقل:
   - `Find`
   - `Wait`
   - `Invoke`
   - `SetValue`
   - helpers الخام مثل:
     - `Get-UiDescendants`
     - `Get-UiBounds`
     - `Get-UiElementSummary`
     - `Get-UiClickableAncestor`
3. حذف التكرار من [UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1) والإبقاء عليه كـ facade متوافقة
4. تثبيت ترتيب التحميل:
   - `Core`
   - ثم `Windows`
   - ثم `Dialogs`

### المخاطر

- بطء غير مقصود بعد النقل
- كسر fallback logic القديم

### بوابة النجاح

- `Elements`
- `SetField`
- `Click`
- `Key`

تبقى سليمة على نفس الحوارات والمساحات الحالية

### نتيجة التنفيذ

- `Probe` نجحت بعد الفصل
- `All` نجحت على جلسة نظيفة بعد الفصل
- لم يعد `UIAutomation.Acceptance.psm1` يحمل كتلة الـ Core المكررة داخله

---

## المرحلة 3 — تخفيف `ui_explore.ps1`

**الحالة:** مكتملة

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
2. نقل logic الثقيل إلى modules:
   - [scripts/modules/UiAutomation.Session.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Session.ps1:1)
   - [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
   - [scripts/modules/UiAutomation.Actions.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Actions.ps1:1)
3. استبدال `Invoke-UiAction` الثقيلة بنداء واحد إلى `Invoke-UiExploreAction`
4. تقسيم المساعدات المحلية إلى:
   - session helpers
   - payload builders
   - dispatch

### بوابة النجاح

- الملف ينخفض بوضوح
- فهم المسار من `Action` إلى التنفيذ يصبح مباشرًا
- تبقى الأوامر التالية سليمة:
  - `Probe`
  - `Sidebar`
  - `Click`
  - `SetField`
  - `DialogAction`

### نتيجة التنفيذ

- `ui_explore.ps1` انخفضت من نحو `597` سطرًا إلى نحو `98`
- الواجهة الخارجية للأداة بقيت كما هي
- التحقق الحي نجح في:
  - `Probe`
  - `Sidebar`
  - `Click`
  - `SetField`
  - `DialogAction`

---

## المرحلة 4 — طبقة Diagnostics وPerformance

**الحالة:** مكتملة

### الهدف

فصل التشخيص عن التفاعل.

### العمل

1. توسيع [scripts/modules/UiAutomation.Diagnostics.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Diagnostics.ps1:1)
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

### نتيجة التنفيذ

- انتقل قلب التشخيص فعليًا من `UIAutomation.Acceptance.psm1` إلى `UiAutomation.Diagnostics.ps1`
- بقيت مخرجات:
  - `Probe`
  - `Diagnostics`
  - `State`
  متوافقة وظيفيًا
- التحقق الحي نجح في:
  - `Probe`
  - `Sidebar`
  - `All` على جلسة نظيفة

---

## المرحلة 5 — طبقة Capture & Visual Evidence

**الحالة:** مكتملة

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

### نتيجة التنفيذ

- انتقلت دوال:
  - `Save-UiWindowScreenshot`
  - `Save-UiDesktopScreenshot`
  - `New-UiContactSheet`
  - `Compare-UiImages`
  من `UIAutomation.Acceptance.psm1` إلى:
  - [scripts/modules/UiAutomation.Capture.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Capture.ps1:1)
- وبقي الـ facade متوافقًا من الخارج مع نفس أوامر الـ API العامة
- كما أصبحت unit regression تغطي:
  - إنشاء `contact sheet`
  - اكتشاف الفرق بين صورتين
  - إنشاء `diff image`

---

## المرحلة 6 — اختبار الأداة نفسها

**الحالة:** توسعت عمليًا

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

- `Probe` على جلسة نظيفة
- التنقل إلى `الضمانات`
- فتح `إجراء جديد`
- إدخال قيمة
- إظهار `تأكيد الإغلاق`
- حسم التأكيد عبر `DialogAction`
- التأكد من عودة الجلسة إلى نافذة رئيسية واحدة

### أول تنفيذ فعلي

- أضفنا:
  - [scripts/tests/UiAutomation.Tooling.Smoke.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Smoke.ps1:1)
  - [scripts/tests/UiAutomation.Tooling.Integration.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Integration.ps1:1)
  - [scripts/run_ui_tooling_regression.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_tooling_regression.ps1:1)
  - action عامة جديدة: `WaitWindowClosed`
- وتغطي الجولة الحالية:
  - `Probe`
  - `Sidebar`
  - `Elements`
  - `WaitWindow`
  - `WaitWindowClosed`
  - `SetField`
  - `DialogAction`
  - `Events`
  - `popup/menu resolution`
  - `external window detection`
- والتحقق الحي الحالي:
  - smoke: `10/10`
  - integration: `14/14`
  - وتشغيل `-Suite All` يمر بنجاح
- والمخرجات المرجعية الحالية:
  - [UIAcceptance/latest/tooling-regression-summary.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/tooling-regression-summary.md)
  - [UIAcceptance/latest/tooling-integration-summary.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/latest/tooling-integration-summary.md)

### بوابة النجاح

- نستطيع تغيير الأداة بثقة أعلى
- نعرف بسرعة إذا كسرنا behavior سابقًا

---

## المرحلة 7 — تثبيت API واستخدامها داخليًا

**الحالة:** بدأت عمليًا

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

### أول تنفيذ فعلي

- صار العقد الحي داخل الكود نفسه عبر:
  - [Get-UiSupportedApi](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1)
- وأضفنا مرجعًا توثيقيًا مباشرًا:
  - [UIAutomation_Supported_API.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Supported_API.md:1)
- كما أضفنا suite خفيفة للأجزاء الثابتة:
  - [scripts/tests/UiAutomation.Tooling.Unit.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Unit.ps1:1)
- ثم أضفنا suite مستقلة لقياس حرية الاستكشاف نفسها:
  - [scripts/tests/UiAutomation.Tooling.Freedom.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/tests/UiAutomation.Tooling.Freedom.ps1:1)
- والـ runner الحالي يدعم الآن:
  - `Smoke`
  - `Integration`
  - `Unit`
  - `Freedom`
  - `All`
- وهذه suite لا تقيس فقط "هل الأمر نجح؟"
  - بل تقيس:
    - هل ما زال الوصول لنفس النتيجة ممكنًا عبر أكثر من modality
    - وهل الأداة تتجنب الانزلاق إلى path واحدة مقيدة مع الزمن

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
