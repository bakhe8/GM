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

- `استئناف آخر ضمان`
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

- `آخر ضمان`
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
- أفعال الصف:
  - `عرض`
  - الانتقال إلى بيت العمل المناسب
- النسخ ليس في الصف؛ مكانه لوحة التفاصيل.
- handoff إلى:
  - `اليوم` نفسها
  - `الطلبات`
  - `الضمانات`
  - `التقارير`

#### Detail Panel

- `Dashboard.Detail.PrimaryActionButton`
- `Dashboard.Detail.OpenWorkspaceButton`
- `Dashboard.Detail.Header.CopyTitle`
- `Dashboard.Detail.CopyReference`
- معلومات:
  - العنوان
  - التصنيف / نوع المتابعة
  - الأولوية / المستوى
  - المرجع
  - الاستحقاق / المدة
  - المساحة المستهدفة / المسار
  - الإجراء الموصى به / الإجراء المقترح
- صفوف المعلومات تتبع نمط `DetailFactLine/DetailFactBlock`: أيقونة صغيرة، فراغ ثابت، اسم الحقل، قيمة، وزر نسخ شفاف في العمود الأخير.

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
- لا توجد قائمة `نسخ` في الصف؛ النسخ يكون من لوحة التفاصيل عبر صفوف المعلومات.
- تصدير الدفعات خرج من قائمة الصف إلى toolbar `إنشاء وتصدير`
- تفاصيل الطلب وخطاب الطلب في لوحة التفاصيل، وليست داخل قائمة الصف.
- سجل الضمان وفلترة طلبات نفس الضمان ليست داخل قائمة الصف حتى لا تتكرر مع بيوت أخرى أو أزرار تفصيلية

#### Detail Panel

- `Requests.Detail.PrimaryActionButton`
- `Requests.Detail.OpenGuaranteeButton`
- `Requests.Detail.OpenLetterButton`
- `Requests.Detail.Header.CopyGuaranteeNo`
- `Requests.Detail.Header.CopySupplier`
- `Requests.Detail.CopyReference`
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

البيت العام للمحفظة والفلترة والاختيار، مع لوحة جانبية مفتوحة دائمًا لبيانات الضمان ومرفقاته وإجراءاته المختصرة.

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
- row context menu بعد الجرد أصبح مختصرًا، ولا يحمل أفعال الطلبات أو التقارير

#### Row Context Menu بعد الجرد

قائمة سهم الصف لم تعد مكانًا جامعًا لكل شيء. بقي فيها فقط ما يخص الضمان كسجل مرجعي:

- `عرض المرفقات`
- `استعلامات تشغيلية`
- `نسخ`

وخرج منها:

- `تسجيل رد البنك` إلى `Requests`
- `إنشاء طلب` إلى toolbar `Requests`
- `طلبات الضمان` لأن زر الصف المباشر يؤدي هذا الدور
- `سجل الضمان` لأن له زر صف مباشر
- `تعديل الضمان` لأن مكانه المختصر هو لوحة التفاصيل الجانبية
- `تصدير` لأن مكانه الطبيعي `Reports` أو أزرار التصدير العامة

### القراءة المعمارية

- هذا surface **اختيار ومحفظة**
- `GuaranteeDetailPanel` جزء ظاهر منه، وليس نسخة مخفية أو نافذة مستقلة
- عند الحاجة لطلبات أو ردود بنك، يكون البيت الرسمي هو `Requests`

---

## 6) لوحة تفاصيل الضمان (`GuaranteeDetailPanel`)

### الدور

مكوّن واحد ظاهر داخل `الضمانات` فقط:

- يعرض بيانات الضمان المرجعية.
- يعرض التسلسل الزمني والمرفقات الرسمية.
- يوفر ثلاثة أفعال مختصرة فقط: `تعديل`، `تمديد`، `إفراج`.
- لا يحتوي أقسامًا مخفية لنافذة مستقلة.

### المكونات الرئيسية

#### Root / Navigation

- `GuaranteeDetailPanel.Root`
- `GuaranteeDetailPanel.ScrollViewer`
- `GuaranteeDetailPanel.Header.CopyGuaranteeNo`
- `GuaranteeDetailPanel.Header.CopyBeneficiary`

#### Reference Facts

- `GuaranteeDetailPanel.ReferenceFacts`
- النوع
- المرجع
- تاريخ الإصدار
- تاريخ الانتهاء
- الأيام المتبقية
- هذه الكتلة هي معيار عرض معلومات اللوحات الجانبية في كل الواجهات:
  - أيقونة صغيرة قبل اسم الحقل مع فراغ ثابت
  - القيمة في عمود مستقل
  - زر نسخ شفاف في العمود الأخير لكل صف قيمة
  - مساحة الضغط أكبر من الأيقونة، لكن الأيقونة نفسها صغيرة وبلا حدود

#### Timeline Section

- `GuaranteeDetailPanel.Section.Timeline`
- `GuaranteeDetailPanel.Timeline.ShowAllRequests`
- يعرض آخر ما تغيّر، ويرسل المستخدم إلى `Requests` عند الحاجة إلى الطلبات الكاملة.

#### Attachments Section

- `GuaranteeDetailPanel.Section.Attachments`
- `GuaranteeDetailPanel.Attachments.ShowAll`
- مخصص للأدلة الرسمية الثابتة على الضمان.
- اختصارات خطاب الطلب ورد البنك تبقى عبورًا إلى المخرجات، لا إعادة بناء لقسم طلبات داخل الضمانات.

#### Actions Section

- `GuaranteeDetailPanel.Section.Actions`
- الأفعال:
  - `Edit`
  - `Extension`
  - `Release`

### ما أُزيل عمدًا

- `GuaranteeFileDialog`
- أقسام `DetachedFileOnly`
- أقسام `SidePanelOnly`
- أقسام الطلبات وردود البنك والمخرجات داخل لوحة الضمان
- أي تفرع يجعل الكود يعرض شيئًا ويخفي شيئًا آخر حسب مكان الاستضافة

### القراءة المعمارية

- اللوحة الجانبية وسيلة وصول سريع للمعلومة، لا بيت تنفيذ للطلبات.
- الطلبات وردود البنك وخطابات الطلب بيتها الرسمي هو `Requests`.
- أي عودة لنافذة ملف مستقلة تعتبر regression معماريًا.

---

## 7) نافذة ملف الضمان (`GuaranteeFileDialog`)

### الحالة

أُزيلت، لا أُخفيت.

### سبب الإزالة

- ما بقي فيها كان يخص الطلبات وردود البنك والمخرجات.
- هذه الأشياء لها بيت رسمي ظاهر هو `Requests`.
- الإخفاء/الإظهار بين لوحة جانبية ونافذة مستقلة كان يجعل الكود أكبر من الواجهة الفعلية.

---

## 8) التقارير (`ReportsWorkspaceSurface`)

### الدور

بيت التقارير:

- إنشاء التقارير
- فتح آخر ملف ناتج
- تصدير تقارير المحفظة والتقارير التشغيلية

### المكونات الرئيسية

#### Home Header

- عنوان البيت:
  - `التقارير`
- توضيح الدور:
  - تقارير المحفظة والعمليات والتصدير

#### Toolbar

- `Reports.Toolbar.Reset`
- `Reports.Filter.Category`
- `كل التقارير`
- `تقارير المحفظة`
- `تقارير تشغيلية`
- `Reports.SearchBox`

#### Metrics

- `تقارير المحفظة`
- `تقارير تشغيلية`
- `إجمالي التقارير`
- `جاهزية التقارير`

#### Table

- `Reports.Table.List`
- أعمدة:
  - الإجراءات
  - الفئة
  - الحالة
  - المفتاح
  - عنوان التقرير

#### Detail Panel

- `Reports.Detail.Header.CopyTitle`
- `Reports.Detail.RunButton`
- `Reports.Detail.OpenButton`
- معلومات:
  - المفتاح التشغيلي
  - نوع التقرير
  - جاهزية التقرير
  - آخر ملف ناتج

### القراءة المعمارية

- هذا هو البيت الرسمي للتقارير والتصدير.
- صف التقرير يحدد التقرير فقط؛ تشغيل التقرير وفتح آخر ملف ناتج يتمان من لوحة التفاصيل.
- لا يفتح `البنوك` من داخله؛ `البنوك` بيت مستقل في السايدبار.

---

## 9) البنوك (`BanksWorkspaceSurface`)

### الدور

بيت التعامل مع البنوك وقراءة توزيع الضمانات حسب البنك.

### موضعها الحالي

- عنصر رئيسي في السايدبار
- لها نفس نمط الأسطح المرجعية: header + toolbar + metrics + table + detail panel

### المكونات الرئيسية

#### Home Header

- عنوان البيت:
  - `البنوك`
- توضيح الدور:
  - التعامل مع البنوك وتوزيع الضمانات حسب البنك والقيمة والحالة

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
- زر الصف `عرض`:
  - يفتح واجهة `الضمانات`
  - يضبط فلتر البنك على البنك المحدد
  - يعرض كل ضمانات هذا البنك فقط
- لا يوجد زر `نسخ` في الصف؛ النسخ بقي في لوحة التفاصيل فقط إذا احتاج المستخدم قيمة محددة

#### Detail Panel

- `Banks.Detail.Header.CopyTopBeneficiary`
- صفوف المعلومات تحمل زر نسخ شفاف لكل قيمة.
- فعل اللوحة السفلي الأساسي: عرض ضمانات البنك داخل `الضمانات` مع فلتر البنك.
- معلومات:
  - إجمالي القيمة
  - الحصة
  - عدد الضمانات
  - نشط
  - قريب الانتهاء
  - منتهي

### القراءة المعمارية

- تقرأ كبيت مستقل للتعامل مع البنوك.
- لا تختبئ داخل التقارير، ولا تحتاج زرًا وسيطًا من `Reports`.

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

- `Settings.Detail.Header.CopyTitle`
- title / subtitle / badge
- state
- action
- path
- open path
- `Settings.Detail.CopyPath`
- `Settings.Detail.CopyOpenPath`
- النسخ داخل صف المعلومة نفسه، وليس زرًا سفليًا مكررًا.

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
