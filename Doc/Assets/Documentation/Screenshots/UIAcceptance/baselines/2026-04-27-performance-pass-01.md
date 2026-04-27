# Performance Pass 01 — 2026-04-27

## الهدف

بدء جولة الأداء بعد إغلاق `preview.2 polish` السلوكي، مع التفريق بين:

- بطء البرنامج نفسه
- وبطء الأداة / طريقة القياس

## ما الذي عدّلناه

### 1. انتظار أذكى في `Sidebar`

في [scripts/UIAutomation.Acceptance.psm1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1):

- أزلنا الاعتماد على نوم ثابت `500ms`
- واستبدلناه بانتظار ذكي حتى تتحدث `ShellState` إلى مساحة العمل المطلوبة

### 2. تقليل تهدئة ما بعد التفاعل

في نفس الملف:

- خففنا delays الثابتة بعد:
  - `InvokePattern`
  - `SelectionItemPattern`
  - نقرات fallback
- من غير أن نلغي فترات التهدئة بالكامل

### 3. عتبات زمنية حسب نوع الإجراء

أضفنا تعريفًا أوضح في:

- [scripts/ui_human_calibration.json](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_human_calibration.json:1)

بحيث لا يُعامل:

- `Launch`
- و`Sidebar`
- و`Click`
- و`DialogAction`

بنفس سقف `Probe` أو `Elements`.

### 4. تقييم أصدق في `Probe`

في [scripts/ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1):

- صار `Assessment` يقرأ عتبة الإجراء نفسه، لا العتبة العامة فقط

## التحقق الذي أُجري

### A. تشغيل `All` على جلسة جديدة

```powershell
.\scripts\run_ui_acceptance.ps1 -Scenario All -ReuseRunningSession:$false
```

### B. جولة أوسع فوقها

جرّبنا حيًا:

1. `الضمانات`
2. تشغيل `Operational Inquiry`
3. فتح `Next Step`
4. فتح `HistoryDialog`
5. إغلاق `HistoryDialog`
6. إغلاق `OperationalInquiryDialog`

### C. قياس Launch/Sidebar وحدهما على دورة نظيفة

نفذنا:

1. `Launch`
2. `Sidebar -> الضمانات`
3. `Probe`

## النتائج

### قبل التعديل

من الجولات السابقة:

- `Launch`: حوالي `4379ms`
- `Sidebar`: حوالي `2072ms`
- `Click`: بين `~850ms` و`1081ms`، وأحيانًا بمتوسط قريب من `935ms`

### بعد التعديل

#### في الجولة الأوسع

- `Sidebar`: `1146.37ms`
- `Click` داخل الحوارات الثقيلة:
  - `596.47ms`
  - `406.09ms`
  - `394.31ms`
  - `408.24ms`
- `WaitWindow`: حوالي `240–260ms`
- `SlowActionCount`: `0`

#### في دورة Launch/Sidebar النظيفة

- `Launch`: `3617.39ms`
- `Sidebar`: `1414.68ms`

## الحكم

### ما الذي أُغلق

- لم يعد `Launch` يُصنّف ظلمًا كأنه بطيء جدًا لمجرد أنه أبطأ من `Click`
- `Sidebar` تحسنت فعليًا، لا شكليًا
- `Click` في المسارات الثقيلة نزلت بوضوح إلى منطقة صحية

### ما الذي بقي تحت المراقبة

- `Sidebar` ما زالت قريبة من الحد المقبول في بعض التشغيلات الباردة
- لكنها لم تعد في منطقة `slow`

## الخلاصة

هذه الجولة لم تُنهِ الأداء بالكامل، لكنها:

1. أغلقت أول اختناق واضح في الأداة
2. جعلت القياس نفسه أصدق
3. خففت زمن التنقل والتفاعل في المسارات المتكررة

والخطوة التالية بعد هذه الجولة:

- **UAT أوسع على بيانات أثقل أو حقيقية/معقمة**
- ثم نقرر إن كان ما بقي هو:
  - تحسين أدواتي فقط
  - أم bottleneck حقيقي في التطبيق نفسه
