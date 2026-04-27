# UI Automation Adaptive Capabilities Roadmap

**تاريخ الإنشاء:** 2026-04-27  
**النطاق:** جعل أداة الاستكشاف الحر قادرة على استدعاء قدرات ثقيلة لحظيًا ثم العودة إلى وضعها الخفيف  
**الحالة:** مرجع تنفيذي حي  
**المرجع المعماري الأساسي:** [UIAutomation_Tooling_Roadmap.md](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UIAutomation_Tooling_Roadmap.md:1)

---

## الهدف

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

2. **التفعيل اللحظي**
   - أي capability يجب أن تُفعل داخل نفس الاستكشاف، لا عبر تشغيل منفصل

3. **المدة المحدودة**
   - كل capability تعمل بنظام lease / expiry / auto-stop

4. **الاستمرارية**
   - تفعيل capability لا يجب أن يكسر الجلسة أو يخرجنا من المسار الحالي

5. **الدليل المتصل بالسياق**
   - كل صورة/فيديو/أثر يجب أن يرتبط بـ:
     - action
     - window
     - shell state
     - timestamp
     - session

6. **التدرج**
   - نبدأ بالقدرات الأرخص والأكثر فائدة
   - ثم نضيف الفيديو/الصوت/الماوس المتقدم فوقها

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
- `VideoCapture` — **planned**
- `AudioCapture` — **planned**
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
