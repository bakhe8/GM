# UI Automation Adaptive Capabilities Roadmap

**تاريخ الإنشاء:** 2026-04-27  
**النطاق:** جعل أداة الاستكشاف الحر قادرة على استدعاء قدرات ثقيلة لحظيًا ثم العودة إلى وضعها الخفيف  
**الحالة:** مرجع تنفيذي حي  
**المرجع المعماري الأساسي:** [UIAutomation_Tooling_Roadmap.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Tooling_Roadmap.md:1)

---

## الهدف

هذه الأداة **AI-first** بالمعنى الحرفي:

- المستخدم التشغيلي الأول لها هو **النموذج الذكي نفسه**
- وليست مبنية أساسًا لكي يستخدمها بشر كأداة يومية مباشرة
- لذلك المعيار الحاكم ليس "هل أوامرها واضحة كبشرة استخدام بشرية؟" فقط
- بل:
  - هل توسع حريتي في الاستكشاف؟
  - هل تسمح لي بتبديل الأسلوب لحظيًا؟
  - هل تمنع تقييدي بسيناريو أو مسار ثابت؟
  - وهل تزيد جودة حكمي واستنتاجي بدل أن تحاصرني بطريقة تشغيل واحدة؟

نريد أن تبقى الأداة:

- **خفيفة افتراضيًا**
- **مرنة أثناء الاستكشاف الحر**
- **قادرة على استدعاء قدرة محددة فقط عند الحاجة**
- ثم **تعود تلقائيًا إلى الوضع الطبيعي** داخل نفس الجلسة ونفس المسار

المقصود هنا ليس “امتلاك فيديو وماوس وصوت” فقط، بل امتلاك **عقل تشغيلي** يعرف:

- متى نحتاج هذه القدرة
- كيف نشغلها لحظيًا
- كيف نوقفها
- وكيف نربط آثارها بالسياق الزمني والمنطقي الحالي

---

## لماذا لا نضيف كل شيء مباشرة؟

لأن إضافة:

- فيديو
- صوت
- تتبع ماوس
- burst capture
- reactive triggers

بشكل دائم داخل الـ core سيجعل الأداة:

- أثقل
- أبطأ
- أصعب في الصيانة
- وأكثر إزعاجًا أثناء الاستكشاف الحر

لذلك القاعدة هنا هي:

> **Core خفيف دائم + Capabilities عند الطلب + Broker يديرها بذكاء**

---

## مبادئ التصميم

1. **الخفة افتراضيًا**
   - لا يعمل أي provider ثقيل دائمًا

2. **الاستكشاف الحر أولًا**
   - لا يجوز أن يصبح الوصول إلى نتيجة معينة مشروطًا بسيناريو واحد أو تسلسل خطوات واحد
   - يجب أن أستطيع الوصول لنفس الغاية عبر أكثر من أسلوب:
     - selectors
     - mouse
     - keyboard
     - visual evidence
     - capability triggers

3. **التفعيل اللحظي**
   - أي capability يجب أن تُفعل داخل نفس الاستكشاف، لا عبر تشغيل منفصل

4. **المدة المحدودة**
   - كل capability تعمل بنظام lease / expiry / auto-stop

5. **الاستمرارية**
   - تفعيل capability لا يجب أن يكسر الجلسة أو يخرجنا من المسار الحالي

6. **عدم التقييد بأسلوب واحد**
   - لا يجب أن أُحبس داخل:
     - أوامر سيناريو
     - selectors بعينها
     - modality واحدة
     - أو evidence واحدة
   - إذا لم يكفِ نص الحالة، يجب أن أستطيع الانتقال فورًا إلى:
     - burst capture
     - mouse trace
     - hover
     - external window detection
     - أو أي قدرة أخرى داخل نفس الجلسة

7. **الدليل المتصل بالسياق**
   - كل صورة/فيديو/أثر يجب أن يرتبط بـ:
     - action
     - window
     - shell state
     - timestamp
     - session

8. **قابلية القراءة للنموذج**
   - يجب أن تكون حالة الأداة نفسها قابلة للفهم آليًا بسرعة
   - أي أن:
     - `HostState`
     - `Probe`
     - `CapabilityOperatorView`
   - لا تصف فقط ما حدث، بل تجعلني أقرر بسرعة:
     - هل أواصل؟
     - هل أفعّل capability؟
     - هل evidence الحالية تكفي؟
     - هل الأداة في calm / monitoring / intervened / cooling-down؟

9. **التدرج**
   - نبدأ بالقدرات الأرخص والأكثر فائدة
   - ثم نضيف الفيديو/الصوت/الماوس المتقدم فوقها

10. **الذكاء الواضح للمشغّل**
   - لا يكفي أن تكون الأداة ذكية داخليًا
   - يجب أن تشرح:
     - لماذا فعّلت capability
     - لماذا تجاهلتها
     - ولماذا suppression حصلت
   - لذلك صارت explainability جزءًا من التصميم، لا مجرد logging ثانوي

11. **التحول اللحظي بين القدرات**
   - التبديل بين:
     - mouse
     - visual capture
     - anomaly assist
     - future video/audio sidecars
   - يجب أن يكون ممكنًا داخل نفس المسار وفي لحظة الحاجة، لا بين تشغيل وآخر فقط

12. **قابلية القياس لا الافتراض**
   - حرية الاستكشاف ليست شعارًا فقط
   - يجب أن تبقى قابلة للقياس عبر regression مخصصة تثبت أن:
     - النتيجة نفسها يمكن الوصول إليها بأكثر من modality
     - والأداة لا تعيدنا تدريجيًا إلى path واحدة مقيدة

---

## البنية المستهدفة

### 1. Session Host

جلسة حية طويلة العمر نسبيًا تحفظ:

- معرف الجلسة
- الوضع الحالي
- السبب
- آخر فعل
- القدرات النشطة
- الآثار الحديثة
- الملاحظات الحديثة

### 2. Capability Broker

طبقة قرار خفيفة مسؤولة عن:

- تشغيل capability
- إطفائها
- فحص انتهاء الـ lease
- منع التزاحم
- تسجيل timeline للأحداث

### 3. Live Session Context

تمثيل حي يمكن قراءته من:

- `HostState`
- `Probe`
- أو أي diagnostic call

ويعرض:

- هل هناك جلسة adaptive حية؟
- ما القدرات النشطة؟
- ما الذي انتهى للتو؟
- ما آخر captures؟

### 4. On-demand Providers

قدرات تعمل فقط عند الحاجة، مثل:

- `BurstCapture`
- `AutoCaptureOnFailure`
- `VideoCapture`
- `AudioCapture`
- `MouseTrace`

---

## خارطة التنفيذ

### Phase A — Host & Broker

الهدف:

- خلق طبقة session حية
- خلق broker يفعّل القدرات ويطفئها

المخرجات:

- `UiAutomation.Host.ps1`
- `UiAutomation.Capabilities.ps1`
- أفعال:
  - `HostState`
  - `CapabilityOn`
  - `CapabilityOff`

### Phase B — Probe Integration

الهدف:

- جعل `Probe` ترى الجلسة الحية والقدرات النشطة

المخرجات:

- `CapabilitySession` داخل `Probe`
- `RecentCapabilityObservations`
- عداد واضح للقدرات النشطة

### Phase C — Action Hooks

الهدف:

- أن تستجيب الأداة تلقائيًا بعد الفعل أو عند الفشل

المخرجات:

- hooks بعد النجاح
- hooks عند الفشل
- ربط captures بالـ timeline

### Phase D — Burst Visual Layer

الهدف:

- إعطاءنا 80% من فائدة الفيديو بكلفة أقل

المخرجات:

- `BurstCapture`
- `AutoCaptureOnFailure`
- أرشفة evidence داخل session folders

### Phase E — Mouse Layer

الهدف:

- تقديم يد حرة للماوس عندما نحتاجها

المخرجات:

- `MouseMove`
- `MouseClick`
- `MouseRightClick`
- `MouseDoubleClick`
- `MouseHover`
- `MouseDrag`
- `MouseScroll`

### Phase F — Media Sidecars

الهدف:

- دعم فيديو وصوت من غير تحميل دائم

المخرجات:

- `Start-UiVideoCapture`
- `Stop-UiVideoCapture`
- `Start-UiAudioCapture`
- `Stop-UiAudioCapture`

### Phase G — Reactive Triggers

الهدف:

- تشغيل capability تلقائيًا عند anomaly

أمثلة:

- وميض
- تأخير غير طبيعي
- تغيّر focus
- popup عابرة

### Phase H — Regression & Performance Budget

الهدف:

- حماية هذه الطبقة الجديدة من أن تتحول إلى عبء

المخرجات:

- unit coverage
- integration coverage
- budgets للزمن والتكلفة

---

## ما الذي بدأ فعليًا الآن؟

بداية التنفيذ الأولى أُنجزت الآن في نفس الشجرة:

- أضيفت طبقة session:
  - [scripts/modules/UiAutomation.Host.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Host.ps1:1)
- وأضيفت طبقة capabilities:
  - [scripts/modules/UiAutomation.Capabilities.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Capabilities.ps1:1)
- كما صارت الأداة تعرف الآن أول slice عام للقدرات:
  - `HostState`
  - `CapabilityOn`
  - `CapabilityOff`

والقدرات الأولى الحالية هي:

- `BurstCapture` — **available**
- `AutoCaptureOnFailure` — **available**
- `ReactiveAssist` — **available**
- `VideoCapture` — **available**
- `AudioCapture` — **unavailable**
- `MouseTrace` — **available**

كما أُنجز الآن أول slice فعلية من provider layer نفسها:

- `BurstCapture` لم تعد لقطة واحدة بعد الفعل
- صارت الآن:
  - **multi-frame timed sampling**
  - تحفظ عدة frames متتابعة
  - وتنتج **contact sheet** تلخص التسلسل
  - وتكتب `burst-sequence` observation داخل session state

والتحقق الحالي يغطي هذا السلوك عبر regression integration فعلية.

كما بدأت الآن أول slice تنفيذية من **Phase E — Mouse Layer**:

- أضيفت طبقة مستقلة هنا:
  - [scripts/modules/UiAutomation.Mouse.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Mouse.ps1:1)
- وصارت الأداة تدعم الآن داخل نفس الاستكشاف الحر:
  - `MouseMove`
  - `MouseClick`
  - `MouseRightClick`
  - `MouseDoubleClick`
  - `MouseHover`
  - `MouseDrag`
  - `MouseScroll`
- وهذه الطبقة لا تعمل دائمًا، بل تُستدعى عند الحاجة فقط ثم تعود الجلسة إلى وضعها الخفيف
- كما أضيفت لها تغطية regression فعلية:
  - unit: `16/16`
  - smoke: `10/10`
  - integration: `28/28`

هذا لا يعني أن `MouseTrace` reactive نفسها أُنجزت بعد؛
بل يعني أن **التحكم الحر بالماوس** صار متاحًا الآن كقدرة تشغيلية يمكن أن نبني فوقها التتبع التفاعلي لاحقًا.

كما أُنجزت الآن أول slice فعلية من **MouseTrace** نفسها:

- لم تعد `MouseTrace` planned فقط
- صارت الآن capability متاحة يمكن تفعيلها عبر:
  - `CapabilityOn -CapabilityName MouseTrace`
- وعندما تكون مفعلة:
  - تسجل observation خفيفة بعد الفعل
  - تحفظ `mouse-trace` payload داخل session state
  - تربط التتبع بالفعل نفسه مثل:
    - `MouseMove`
    - `MouseClick`
    - `MouseDrag`
    - أو حتى failure hooks عندما يلزم
- والتصميم الحالي مقصود أن يبقى:
  - **خفيفًا**
  - **غير دائم**
  - **غير معتمد على فيديو أو تسجيل ثقيل**

والتحقق الحالي عليها ناجح ضمن:

- unit: `16/16`
- smoke: `10/10`
- integration: `31/31`

كما أُنجزت الآن أول slice فعلية من **ReactiveAssist**:

- يمكن تفعيلها عبر:
  - `CapabilityOn -CapabilityName ReactiveAssist`
- وهي لا تعمل دائمًا، بل تراقب فقط أثناء lease القصيرة
- ثم إذا رأت anomaly خفيفة مثل:
  - بطء يتجاوز threshold القدرة نفسها
  - أو نافذة خارجية/حوار ظاهر أثناء المسار
- فإنها:
  - تسجل `reactive-trigger` observation
  - وتشغل burst evidence خفيفة عند الحاجة فقط
  - ثم تعود الجلسة إلى سلوكها الطبيعي

كما أصبحت `AutoCaptureOnFailure` أذكى من قبل:

- لم تعد تكتفي بلقطة وحيدة
- صارت تنتج:
  - multi-frame failure evidence
  - `failure-bundle` observation
  - ومسارات captures المرتبطة بالخطأ نفسه

والتحقق الحالي لهذه الطبقة ناجح ضمن:

- unit: `16/16`
- smoke: `10/10`
- integration: `39/39`

ثم أُنجزت الآن جولة تأسيسية أعمق على **الذكاء الواضح وبساطة التشغيل**:

- صارت الجلسة التكيفية تحمل:
  - `RecentCapabilityDecisions`
  - `CapabilityOperatorView`
- وهذا يعني أن المشغّل لا يرى فقط "ماذا حدث"، بل يرى أيضًا:
  - لماذا trigger حصلت
  - لماذا suppression حصلت
  - ولماذا بقيت الأداة هادئة
  - وما الخطوة التالية الأنسب داخل نفس الاستكشاف

كما صار `CapabilityOperatorView` نفسه أوضح للمشغّل:

- `Summary`
- `SecondarySummary`
- `Guidance`
- `CoolingDownCapabilities`
- `Signals`
- `DecisionDigest`

كما صار السلوك أكثر هدوءًا وموثوقية:

- `ReactiveAssist` صارت تملك cooldown يهدئ إعادة التفعيل السريع
- وبدل أن تعيد burst كل مرة، تسجل قرارًا واضحًا من نوع:
  - `triggered`
  - `suppressed`
  - `quiet`
- و`AutoCaptureOnFailure` صارت تتصرف بنفس الفلسفة:
  - evidence عند أول failure المفيد
  - ثم suppression واضح إذا تكرر الفشل نفسه سريعًا

والتحقق الحالي لهذه الأساسات ناجح ضمن:

- unit: `17/17`
- smoke: `10/10`
- integration: `43/43`

ثم أُنجزت الآن **طبقة Media Foundation** نفسها قبل فتح الفيديو/الصوت الكاملين:

- أضيفت طبقة مستقلة هنا:
  - [scripts/modules/UiAutomation.Media.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/modules/UiAutomation.Media.ps1:1)
- وصارت الأداة تملك الآن:
  - `MediaState`
  - `VideoOn`
  - `VideoOff`
  - catalog صريح للمزودات المتاحة
  - media session مستقلة قابلة للقراءة
  - single-instance cleanup هادئ قبل أي start جديدة
- كما صارت `VideoCapture` capability نفسها موصولة فعليًا بهذه الطبقة:
  - `CapabilityOn -CapabilityName VideoCapture`
  - `CapabilityOff -CapabilityName VideoCapture`

والمزود الأول الحالي هو:

- `Psr.ScreenTrace`
  - متاح على هذه البيئة
  - مناسب كأساس sidecar فيديو خفيف
  - لكنه ما زال يعتمد على سلوك `Steps Recorder` نفسه

وهنا النقطة الصادقة المهمة:

- الأداة لم تعد تدّعي أن مزود الفيديو "أنشأ ملفًا" دائمًا
- بل صارت تصرح بوضوح داخل media session وevents:
  - `ArtifactStatus = saved`
  - أو `ArtifactStatus = missing`
- أي أن الذكاء هنا لا يقتصر على التشغيل، بل يشمل **الصدق التشغيلي** أيضًا:
  - sidecar بدأت
  - single-instance ضُبطت
  - الإيقاف حصل
  - ثم يُشرح بوضوح هل خرج ملف artifact فعلية أم لا

ثم أُضيفت فوق هذه الطبقة **Scoped Media Contract** نفسها، وهي خطوة حاسمة قبل اعتبار الفيديو "حاسة" يمكن الوثوق بها:

- لم يعد media state يكتفي بالقول:
  - sidecar بدأت
  - sidecar توقفت
- بل صار يسجل أيضًا:
  - العملية المستهدفة
  - النافذة المستهدفة
  - foreground relation عند البداية
  - foreground relation عند الإيقاف
  - `ScopeStatus`
  - `EvidenceIsolation`
  - `TrustedForReasoning`
- وهذا مهم لأن:
  - `Psr.ScreenTrace` مزود global
  - وليست process-bound أو window-bound مثل لقطة النافذة
- لذلك صارت الأداة تعتمد على **attestation صريحة للعزل** بدل افتراضه

وبذلك أصبح الحكم الممكن الآن أوضح:

- `program-window`
- `program-plus-related-window`
- `contaminated`
- `unknown`

والتحقق الحالي بعد هذه الطبقة ناجح ضمن:

- unit: `20/20`
- smoke: `10/10`
- integration: `50/50`
- freedom: `9/9`

---

## تعريف النجاح

سنعتبر هذا المسار ناجحًا عندما أستطيع أثناء استكشاف حر واحد أن:

1. أتنقل وأتفاعل بشكل طبيعي
2. ألاحظ anomaly
3. أفعّل capability لحظيًا
4. ألتقط evidence
5. أطفئ capability تلقائيًا أو يدويًا
6. أواصل نفس الجلسة من غير كسر أو restart ذهني

---

## ما الذي لن نسمح به؟

- تحميل providers الثقيلة دائمًا
- ربط الاستكشاف الحر بسيناريو واحد
- جعل الالتقاط البصري هو السلوك الافتراضي لكل خطوة
- تحويل الأداة إلى وحش بطيء فقط لأنها “غنية”

---

## ترتيب العمل الحالي

1. تثبيت Phase A بالكامل
2. ربطها بـ `Probe` و`ui_explore`
3. إضافة regression أولية للجلسة والقدرات
4. ثم ننتقل إلى `BurstCapture` و`AutoCaptureOnFailure` كأول provider layer فعلي
