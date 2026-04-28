# UI Component Atlas

**تاريخ الإنشاء:** 2026-04-28  
**الغرض:** فهرسة الواجهات ومكوّناتها الداخلية والنوافذ الثانوية حتى لا ندخل في إصلاح موضعي ونكتشف لاحقًا أن القرار تجاهل زرًا أو كرتًا أو حوارًا فرعيًا أو نافذة نظامية مرتبطة بالمسار نفسه.

## كيف نقرأ الأطلس

لكل surface أو نافذة نثبت:

1. **الدور**
2. **المكونات الرئيسية**
3. **الأفعال الأساسية**
4. **العناصر التابعة أو النوافذ التي تتفرع منها**
5. **القراءة المعمارية**: هل هذا بيت رسمي؟ عدسة؟ handoff؟ أم حوار تابع؟

---

## 1) Shell / Main Window

### الدور

الحاوية العليا لكل البرنامج.

### المكونات

- `Shell.MainWindow`
- أزرار chrome:
  - `Shell.MaximizeButton`
  - `Shell.MinimizeButton`
- بحث عام:
  - `Shell.GlobalSearchBox`
  - `Shell.GlobalSearchButton`
- جسم الضمانات الأساسي:
  - `Shell.GuaranteesWorkspaceView`
  - `Shell.GuaranteeDetailPanel`
- host للأسطح الثانوية:
  - `Shell.ActiveWorkspaceHost`
- السايدبار:
  - `Shell.Sidebar.Dashboard`
  - `Shell.Sidebar.Guarantees`
  - `Shell.Sidebar.Requests`
  - `Shell.Sidebar.Reports`
  - `Shell.Sidebar.Settings`
- شريط الحالة:
  - `Shell.Status.Primary`
  - `Shell.Status.Secondary`

### القراءة المعمارية

- هذا ليس surface عمل، بل **مستوى النظام كله**
- أي تغيير في السايدبار أو حمل السياق أو status bar ينعكس على كل العائلات

---

## 2) اليوم (`DashboardWorkspaceSurface`)

### الدور

بيت البداية والأولوية وhandoff السريع.

### المكونات الرئيسية

#### Toolbar

- `استئناف آخر ملف`
- `فتح الضمانات`
- `متابعات الانتهاء`
- `تحديث`
- فلتر نطاق اليوم:
  - `Dashboard.Filter.Scope`
  - `أعمال اليوم`
  - `طلبات معلقة`
  - `متابعات الانتهاء`
- بحث:
  - `Dashboard.SearchBox`

#### Metrics

- `آخر ملف`
- `أعمال حرجة`
- `طلبات معلقة`
- `متابعات الانتهاء`

> داخل عدسة `متابعات الانتهاء` نفسها، تتبدل هذه البطاقات إلى:
> - `قريب الانتهاء`
> - `منتهي`
> - `إجمالي القيمة`
> - `أقرب تاريخ`

#### Table / Work List

- `Dashboard.Table.List`
- عناصر عمل يومية منسقة كـ work items
- handoff إلى:
  - `اليوم` نفسها
  - `الطلبات`
  - `الضمانات`
  - `التقارير`

#### Detail Panel

- `Dashboard.Detail.PrimaryActionButton`
- `Dashboard.Detail.OpenWorkspaceButton`
- `Dashboard.Detail.CopyReferenceButton`
- معلومات:
  - العنوان
  - التصنيف / نوع المتابعة
  - الأولوية / المستوى
  - المرجع
  - الاستحقاق / المدة
  - المساحة المستهدفة / المسار
  - الإجراء الموصى به / الإجراء المقترح

### القراءة المعمارية

- `اليوم` ليست شاشة تقارير
- وليست queue كاملة
- هي **بيت قرار البداية**

### عناصر انتقالية داخلها

- متابعات الانتهاء
- الطلبات المعلقة
- handoff إلى بيوت أخرى

---

## 3) التنبيهات (`NotificationsWorkspaceSurface`)

### الحالة الحالية

أُزيل هذا السطح من الكود التشغيلي.

لم يعد يوجد:

- `NotificationsWorkspaceSurface`
- `NotificationsWorkspaceDataService`
- `NotificationsWorkspaceCoordinator`
- `ShellWorkspaceKeys.Notifications`
- `CreateNotificationsWorkspace`

البديل الرسمي الآن هو:

- `اليوم / متابعات الانتهاء`
- `Dashboard.Filter.ExpiryFollowUpKind`
- aliases البحث الشامل:
  - `التنبيهات`
  - `المتابعات`
  - `notifications`
  - `alerts`

### القراءة المعمارية

- لا يوجد surface باسم `Notifications` يجب صقله
- أي مكوّن follow-up جديد يضاف داخل `DashboardWorkspaceSurface`

---

## 4) الطلبات (`RequestsWorkspaceSurface`)

### الدور

Queue-first surface لتنفيذ الطلب الحالي.

### المكونات الرئيسية

#### Toolbar

- `Requests.Toolbar.RegisterResponse`
- `Requests.Toolbar.Refresh`
- `Requests.Toolbar.MoreTools`
- فلتر الحالة:
  - `Requests.Filter.Status`
- بحث:
  - `Requests.SearchBox`

#### Metrics

- `إجمالي الطلبات`
- `قيد الانتظار`
- `بدون مستند رد`
- `مغلق`

#### Table

- `Requests.Table.List`
- أعمدة:
  - الإجراءات
  - الحالة
  - نوع الطلب
  - القيمة المطلوبة
  - القيمة الحالية
  - تاريخ الطلب
  - البنك
  - المورد
  - رقم الضمان

#### Row Actions

- **فعل يومي واحد ظاهر**
- قائمة منسدلة للأفعال الثانوية
- انتقال إلى:
  - فتح الخطاب
  - فتح الرد
  - فتح الضمان
  - سجل الضمان
  - نسخ / أدوات إضافية

#### Detail Panel

- `Requests.Detail.PrimaryActionButton`
- `Requests.Detail.OpenGuaranteeButton`
- `Requests.Detail.OpenLetterButton`
- معلومات:
  - المرجع
  - الإصدار
  - القيمة الحالية
  - القيمة المطلوبة
  - التواريخ
  - ملاحظات الطلب
  - رد البنك

### مكوّن فرعي إضافي

- `RequestsWorkspaceDialog`
  - نافذة قائمة/تفاصيل للطلبات في سياقات محددة

### القراءة المعمارية

- هذه surface تنفيذ يومي
- create/export يجب أن يبقيا فيها بوزن ثانوي

---

## 5) الضمانات (`GuaranteesDashboardView`)

### الدور

البيت العام للمحفظة والفلترة والاختيار قبل الدخول إلى ملف الضمان.

### المكونات الرئيسية

#### Toolbar

- `Guarantees.Toolbar.CreateNew`
- `Guarantees.Toolbar.SmartFilter`
- `Guarantees.Toolbar.ExportVisible`
- فلاتر:
  - `Guarantees.Filter.TimeStatus`
  - `Guarantees.Filter.Bank`
  - `Guarantees.Filter.Type`
- بحث:
  - `Guarantees.SearchBox`

#### KPI Band

- `طلبات معلقة`
- `منتهي`
- `تحتاج متابعة`
- `قريب الانتهاء`
- `نشط`

#### Main Table

- جدول الضمانات الرئيسي
- selection مرتبط مباشرة باللوحة اليمنى
- زر الصف المباشر هو الآن `الطلبات`:
  - يفتح شاشة `الطلبات`
  - ويضع رقم الضمان في البحث حتى تظهر طلبات هذا الضمان فقط
- row context menu واسع جدًا، يضم:
  - التسجيل / التعديل
  - المرفقات
  - الطلبات
  - الأفعال الثقيلة:
    - تمديد
    - إفراج
    - تخفيض
    - تسييل
    - تحقق
    - استبدال
    - نقض
  - التصديرات
  - النسخ

### القراءة المعمارية

- هذا surface **اختيار ومحفظة**
- لكنه ليس وحده “بيت القرار”؛ القرار الحقيقي يتوزع مع `GuaranteeDetailPanel` و`GuaranteeFileDialog`

---

## 6) ملف الضمان (`GuaranteeDetailPanel`)

### الدور

مكوّن مشترك له دوران واضحان:

- داخل `الضمانات`: لوحة جانبية مفتوحة دائمًا للبيانات والتاريخ والمرفقات وثلاثة أفعال مختصرة فقط: `تعديل`، `تمديد`، `إفراج`.
- داخل `GuaranteeFileDialog`: ملف تشغيل غير مكرر للطلبات والاستعلامات والمخرجات وبقية الإجراءات.

### المكونات الرئيسية

#### Root / Navigation

- `GuaranteeDetailPanel.Root`
- `GuaranteeDetailPanel.ScrollViewer`
- `GuaranteeDetailPanel.CloseDetachedFileButton` عند العمل كنافذة مستقلة

#### Start Here + File Map

- تظهر هذه المجموعة في نافذة `ملف الضمان` المنفصلة فقط، ولا تظهر في اللوحة الجانبية.
- `GuaranteeDetailPanel.Section.ExecutiveSummary`
- بداية القرار صارت تظهر أولًا داخل:
  - `GuaranteeDetailPanel.Section.ActionSummary`
  - `GuaranteeDetailPanel.ActionSummary.FocusSuggested`
- خريطة الملف لم تعد روابط مسطحة فقط، بل صارت مجموعتين:
  - `القرار والتنفيذ`
    - `ExecutiveSummary`
    - `Requests`
  - `المراجعة والأثر`
    - `Outputs`

#### Reference Facts

- تظهر في اللوحة الجانبية فقط، ولا تظهر في نافذة `ملف الضمان`.
- `GuaranteeDetailPanel.ReferenceFacts`
- النوع
- المرجع
- تاريخ الإصدار
- تاريخ الانتهاء
- الأيام المتبقية

#### Operational Inquiry

- تظهر في نافذة `ملف الضمان` المنفصلة فقط.
- `GuaranteeDetailPanel.Section.OperationalInquiry`
- `GuaranteeDetailPanel.OperationalInquiry.Options`
- `GuaranteeDetailPanel.OperationalInquiry.Run`

#### Latest Inquiry

- تظهر في نافذة `ملف الضمان` المنفصلة فقط.
- `GuaranteeDetailPanel.Section.LatestInquiry`
- `FocusSuggested`
- `OpenDialog`
- `OpenHistory`
- `OpenLetter`
- `OpenResponse`

#### Requests Section

- تظهر في نافذة `ملف الضمان` المنفصلة فقط.
- `GuaranteeDetailPanel.Section.Requests`
- `GuaranteeDetailPanel.Requests.ShowAll`
- لم تعد تُقرأ كأرشيف طلبات فقط
- صارت تعلن بوضوح:
  - ما الطلب الذي يحرك القرار الآن
  - وما أحدث الطلبات التابعة لنفس السلسلة
- وعند فتح الملف من `Requests` يظهر الطلب القادم من هناك كمرجع التنفيذ الحالي

#### Timeline Section

- تظهر في اللوحة الجانبية فقط، ولا تظهر في نافذة `ملف الضمان`.
- `GuaranteeDetailPanel.Section.Timeline`
- `GuaranteeDetailPanel.Timeline.ShowAllRequests`
- لم يعد الغرض منها “عرض أحداث” فقط
- بل:
  - تفسير آخر ما تغيّر
  - وتثبيت نقطة التوقف قبل مراجعة المخرجات أو المرفقات

#### Outputs Section

- تظهر في نافذة `ملف الضمان` المنفصلة فقط.
- `GuaranteeDetailPanel.Section.Outputs`
- هذا القسم هو بيت:
  - الأثر الناتج عن الطلبات
  - والملفات الجاهزة للفتح فورًا
- لذلك يقرأ الآن كل ما له علاقة بخطاب الطلب أو رد البنك كـ:
  - `مخرجات`
  - لا `مرفقات رسمية`

#### Attachments Section

- تظهر في اللوحة الجانبية فقط، ولا تظهر في نافذة `ملف الضمان`.
- `GuaranteeDetailPanel.Section.Attachments`
- `GuaranteeDetailPanel.Attachments.ShowAll`
- هذا القسم صار مخصصًا للأدلة الرسمية الثابتة على الملف
- بينما اختصارات آخر خطاب طلب وآخر رد بنك تبقى:
  - إشارات عبور سريعة
  - لا إعادة دمج للمخرجات داخل المرفقات

#### Actions Section

- تظهر في اللوحة الجانبية ونافذة `ملف الضمان` لكن بمحتوى مختلف:
  - اللوحة الجانبية تعرض فقط: `Edit`، `Extension`، `Release`
  - نافذة `ملف الضمان` تعرض بقية الإجراءات فقط، ولا تعرض `Edit` أو `Extension` أو `Release`
- `GuaranteeDetailPanel.Section.Actions`
- الطبقات:
  - اللوحة الجانبية:
    - `Edit`
    - `Extension`
    - `Release`
  - نافذة الملف:
    - `RegisterResponse`
    - `Reduction`
    - `Liquidation`
    - `Verification`
    - `Replacement`
    - `Annulment`

### القراءة المعمارية

- هذا هو **البيت الأقوى للقرار**
- ولم تعد الأفعال فيه تقرأ كشبكة مسطحة واحدة؛ صارت هرمية:
  - نفّذ
  - ثم راجع
  - ثم استخدم الأفعال الأثقل عند الحاجة
- أي نقل لأفعال منه يجب أن يكون deliberate جدًا

---

## 7) نافذة ملف الضمان (`GuaranteeFileDialog`)

### الدور

غلاف window مستقل لـ `GuaranteeDetailPanel`.

### المكونات

- عنوان: `ملف الضمان - {GuaranteeNo}`
- محتوى واحد فقط:
  - `GuaranteeDetailPanel`

### القراءة المعمارية

- ليست تجربة جديدة
- بل **نفس البيت التنفيذي** لكن في نافذة مستقلة

---

## 8) التحليلات والمخرجات (`ReportsWorkspaceSurface`)

### الدور

بيت التحليلات والمخرجات:

- إنشاء التقارير
- فتح آخر ملف ناتج
- والدخول إلى العدسات التحليلية الانتقالية مثل `البنوك`

### المكونات الرئيسية

#### Home Header

- عنوان البيت:
  - `التحليلات والمخرجات`
- توضيح الدور:
  - التقارير هي المخرج الأساسي
  - و`البنوك` عدسة تحليلية انتقالية من داخل البيت نفسه
- `Reports.Toolbar.OpenBanksLens`

#### Toolbar

- `Reports.Toolbar.Run`
- `Reports.Toolbar.Reset`
- `Reports.Filter.Category`
- `كل المخرجات`
- `مخرجات المحفظة`
- `مخرجات تشغيلية`
- `Reports.SearchBox`

#### Metrics

- `مخرجات المحفظة`
- `مخرجات تشغيلية`
- `إجمالي المخرجات`
- `جاهزية البيت`

#### Table

- `Reports.Table.List`
- أعمدة:
  - الإجراءات
  - الفئة
  - الحالة
  - المفتاح
  - عنوان المخرج

#### Detail Panel

- `Reports.Detail.RunButton`
- `Reports.Detail.OpenButton`
- معلومات:
  - المفتاح التشغيلي
  - نوع المخرج
  - جاهزية المخرج
  - آخر ملف ناتج

### القراءة المعمارية

- هذا هو البيت الرسمي الحالي لعائلة:
  - `التحليلات والمخرجات`
- وتُفتح منه `البنوك` الآن كعدسة انتقالية تابعة، لا كبيت top-level

---

## 9) البنوك (`BanksWorkspaceSurface`)

### الدور الحالي

عدسة تجميعية على الضمانات حسب البنك.

### موضعها الحالي

- لم تعد عنصرًا في السايدبار العليا
- تُفتح الآن من داخل `التقارير` عبر زر toolbar ثانوي انتقالي

### المكونات الرئيسية

#### Toolbar

- `Banks.Toolbar.Reset`
- `Banks.Filter.Sort`
- `Banks.SearchBox`

#### Metrics

- `عدد البنوك`
- `إجمالي الضمانات`
- `الضمانات النشطة`
- `إجمالي القيمة`

#### Table

- `Banks.Table.List`
- أعمدة:
  - الإجراءات
  - عدد الضمانات
  - نشط
  - قريب الانتهاء
  - منتهي
  - إجمالي القيمة
  - المستفيد الأعلى
  - البنك

#### Detail Panel

- copy actions
- فتح/انتقال حسب الحاجة
- معلومات:
  - إجمالي القيمة
  - الحصة
  - عدد الضمانات
  - نشط
  - قريب الانتهاء
  - منتهي

### القراءة المعمارية

- لا تقرأ كسطح يومي مستقل
- تقرأ كـ **lens تحليلية** ضمن `التحليلات والمخرجات`
- وتُفتح الآن من `التقارير` كعدسة انتقالية، لا من السايدبار العليا

---

## 10) الإعدادات (`SettingsWorkspaceSurface`)

### الدور

بيت التشغيل والصيانة والمسارات والنسخ الاحتياطي.

### المكونات الرئيسية

#### Toolbar

- `Settings.Toolbar.Refresh`
- `Settings.Toolbar.BackupMenu`
  - إنشاء نسخة احتياطية
  - استرجاع نسخة احتياطية
  - إنشاء حزمة محمولة
  - استرجاع حزمة محمولة
- `Settings.Toolbar.ToolsMenu`
  - نسخ ملخص المسارات
  - توليد بيانات تجريبية (في `DEBUG`)
- `Settings.Filter.Category`
- `Settings.SearchBox`

#### Metrics

- `قاعدة البيانات`
- `المرفقات`
- `الخطابات`
- `ردود البنوك`

#### Table

- `Settings.Table.List`
- أعمدة:
  - الإجراءات
  - الحالة
  - الفئة
  - المسار
  - العنصر

#### Detail Panel

- title / subtitle / badge
- state
- action
- path
- open path

### القراءة المعمارية

- Surface تشغيل
- ليست بيت اكتشاف أو تنفيذ يومي

---

## 11) النوافذ الثانوية الرئيسية

### `PromptDialog`

- إدخال نصي بسيط
- confirm / cancel

### `BankResponseDialog`

- selector للطلب
- selector للحالة
- notes
- اختيار مستند رد
- summary للمستند
- حفظ / إلغاء

### `NewGuaranteeDialog`

- حقول الضمان الأساسية
- ملخص المرفقات
- معاينة أثر الحفظ
- أزرار:
  - المرفقات
  - الحفظ
  - التقارير
  - الإلغاء

### `EditGuaranteeDialog`

- نفس بيت `NewGuaranteeDialog` تقريبًا
- مع:
  - ملخص المرفقات الحالية
  - ملخص المرفقات الجديدة
  - معاينة أثر الحفظ

### `ReplacementRequestDialog`

- نافذة authoring متخصصة لطلب الاستبدال

### `HistoryDialog`

- تبويبات:
  - `Versions`
  - `Requests`
- next step card
- detail panel
- أفعال:
  - فتح المرفقات
  - فتح الخطاب
  - فتح الرد
  - تصدير السجل
  - طباعة
  - نسخ رقم الضمان
  - إغلاق

### `OperationalInquiryDialog`

- next step card
- content scroll
- cards:
  - answer
  - facts
  - timeline
- أفعال:
  - فتح السجل
  - فتح المرفقات
  - فتح الخطاب
  - فتح الرد
  - تصدير التقرير
  - إغلاق

### `AttachmentPickerDialog`

- قائمة مرفقات
- فتح

### `RequestsDialog`

- قائمة الطلبات المرتبطة بالضمان
- أفعال:
  - تسجيل رد
  - فتح الخطاب
  - فتح الرد
  - إغلاق

### `GuidedTextPromptDialog`

- prompt موجه
- حقل نصي
- confirm / cancel

### `EligibleGuaranteePickerDialog`

- قائمة ضمانات مؤهلة
- اختيار واحد ثم متابعة

### `AttachResponseDocumentDialog`

- اختيار ملف
- ملخص الملف
- notes
- إلحاق / إلغاء

---

## 12) النوافذ الثانوية المساندة

### `BanksSummaryDialog`

- قائمة مجمعة حسب البنك

### `ReportPickerDialog`

- اختيار تقرير من catalogue قديمة/مساندة

### `SettingsDialog`

- عرض نصي للمسارات

---

## 13) النوافذ النظامية والخارجية التي تدخل ضمن التجربة

هذه ليست جزءًا من XAML/WPF الداخلية، لكنها جزء من المعمارية التشغيلية ويجب حسابها في أي flow map:

- `OpenFileDialog`
- `SaveFileDialog`
- نافذة الطباعة الخارجية:
  - `GuaranteeManager - Print`
- فتح الملفات الخارجية:
  - خطاب الطلب
  - رد البنك
  - ملفات التقرير الناتجة

### لماذا هذا مهم؟

لأن بعض المسارات لا تُفهم كاملًا من داخل surface وحدها:

- `فتح الرد`
- `إلحاق الرد`
- `تصدير التقرير`
- `النسخ الاحتياطي / الاسترجاع`
- `الطباعة`

كلها تعبر عبر نوافذ نظامية أو خارجية، ولذلك يجب أن تبقى جزءًا من الخريطة.

---

## 14) قاعدة العمل بعد هذا الأطلس

قبل أي إصلاح لاحق نسأل:

1. هل هذا العنصر جزء من **بيت رسمي** أم من **عدسة انتقالية**؟
2. هل العنصر يجب أن يبقى داخل surface أم ينتقل إلى:
   - family أكبر
   - detail panel
   - dialog
   - overflow
3. هل النافذة الثانوية جزء من التجربة الأصلية أم تعويض مؤقت؟
4. هل الفعل ظاهر في أكثر من مستوى دون ضرورة؟

> الهدف من هذا الأطلس ليس حفظ الأسماء فقط، بل منعنا من إصلاح الواجهة “جزءًا جزءًا” من غير رؤية النظام كاملًا.
