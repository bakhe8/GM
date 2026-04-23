# Guarantee Manager - Design System Reference

هذا الملف هو المرجع الرسمي الوحيد لتصميم واجهات برنامج `Guarantee Manager`.
الغرض منه توحيد الهوية البصرية، سلوك التفاعل، بنية الشاشات، وقواعد استخدام موارد XAML على مستوى البرنامج كاملًا.

---

## 0. Scope, Authority, and Governance

### 0.1 النطاق الرسمي
هذه الوثيقة تغطي البرنامج كاملًا، وليس الواجهات الرئيسية فقط. ويشمل ذلك:

- `Main Shell` والتنقل الرئيسي.
- مساحات العمل الرئيسية: `Today Desk`, `DataTable / Guarantees`, `Operation Center`, `Settings`.
- الصفحات والملفات التفصيلية: مثل `GuaranteeFileView`.
- النوافذ الحوارية التشغيلية: مثل `Create*RequestWindow`, `RecordWorkflowResponseWindow`, `AttachmentListWindow`, `TextPromptWindow`.
- نوافذ التاريخ والتحليل: مثل `GuaranteeHistoryWindow`, `InquiryResultWindow`.
- الـ side sheets: مثل `GuaranteeSideSheetView`, `RequestSideSheetView`.

### 0.2 السلطة المرجعية
- أي شاشة جديدة أو شاشة يعاد تصميمها يجب أن تلتزم بهذه الوثيقة من أول يوم.
- أي شاشة قديمة لا تزال غير متوافقة تُعد دينًا تصميميًا يجب معالجته، وليست استثناءً معتمدًا.
- لا يجوز إنشاء نمط بصري جديد محليًا داخل شاشة إلا إذا لم يوجد مقابل مناسب في `Themes/*`، وعندها يجب توثيق السبب وإضافة مورد عام لاحقًا إذا تكرر الاستخدام.

### 0.3 سياسة الاستثناءات
- الاستثناءات مسموحة فقط عندما يكون هناك قيد وظيفي أو قيد WPF واضح.
- يجب أن تكون الاستثناءات محدودة النطاق، ومشروحة داخل الكود أو في المراجعة.
- لا يُسمح بتجاوزات عامة مثل ألوان جديدة، زوايا جديدة، أو أحجام خطوط جديدة بدون تحديث هذا الملف والـ theme resources.

---

## 1. Coverage Matrix

| الفئة | أمثلة ممثلة | النمط الإلزامي |
| :--- | :--- | :--- |
| التنقل والهيكل العام | `MainWindow`, main shell, sidebar | خلفية صلبة، RTL، `Button.Nav` أو `Button.Nav.V2`، حالات نشطة واضحة |
| مساحات العمل الرئيسية | `TodayDeskView`, `DataTableView`, `OperationCenterView`, `SettingsView` | بطاقات بيضاء مستقلة فوق `Surface_Bg_Light`، شريط مؤشرات عند الحاجة، بطاقات محتوى رئيسية |
| الصفحات التفصيلية | `GuaranteeFileView` | بطاقة علوية رئيسية، أقسام مستقلة، إجراءات واضحة، عدم تجميع الصفحة كلها داخل كتلة بيضاء واحدة |
| النوافذ الحوارية التشغيلية | `CreateExtensionRequestWindow`, `CreateReleaseRequestWindow`, `RecordWorkflowResponseWindow` | نافذة بلون الخلفية الرسمي، بطاقة عنوان/إرشاد، بطاقة محتوى رئيسية، شريط إجراءات سفلي |
| النوافذ التاريخية والتحليلية | `GuaranteeHistoryWindow`, `InquiryResultWindow` | بطاقة عنوان، بطاقات مؤشرات أو تفسير، بطاقة جدول/نتيجة، إجراءات واضحة |
| الـ side sheets | `GuaranteeSideSheetView`, `RequestSideSheetView` | عرض ثابت، حشو موحد، عنوان مختصر، بطاقات Compact وPanel |
| النوافذ الأداتية الصغيرة | `AttachmentListWindow`, `TextPromptWindow` | تخطيط مختصر، بطاقة واحدة أو اثنتان، لا ازدحام بصري |

---

## 2. Design Tokens

### 2.1 Colors and Surfaces

المفاتيح التالية هي المصدر الرسمي للألوان، كما هي معرفة في `Themes/Base/Brushes.xaml`:

| Resource Key | Value | الاستخدام |
| :--- | :--- | :--- |
| `Brand_Emerald` | `#006847` | الإجراء الأساسي، الحالات النشطة، حدود التفاعل |
| `Brand_Emerald_12` | `#1F006847` | خلفيات التحديد الخفيف |
| `Brand_Emerald_20` | `#33006847` | حدود ولوحات إرشادية خفيفة |
| `Brand_Slate` | `#111111` | نصوص داكنة قوية |
| `Surface_Bg_Light` | `#F5F5F5` | خلفية التطبيق العامة |
| `Surface_Card_White` | `#FFFFFF` | البطاقات والحاويات العائمة |
| `Surface_Sidebar` | `#FAFAFA` | الشريط الجانبي عند الحاجة |
| `Surface_Header` | `#FFFFFF` | الترويسة |
| `Surface_Active` | `#F3F7F5` | hover / active backgrounds |
| `Surface_Border` | `#D6D6D6` | الحدود القياسية |
| `Surface_Border_Strong` | `#B7B7B7` | حدود أقوى للحالات الخاصة |
| `Surface_Scrim` | `#18000000` | طبقة حجب الخلفية |
| `Text_Primary` | `#111111` | العناوين والنصوص المهمة |
| `Text_Secondary` | `#4E4E4E` | النصوص التوضيحية الأساسية |
| `Brush_Text_Muted` | `#6C6C6C` | النصوص الثانوية جدًا |
| `Text_OnBrand` | `#FFFFFF` | نص فوق الخلفيات الخضراء |
| `Status_Success` | `#006847` | نجاح |
| `Status_Warning` | `#E88F00` | تحذير |
| `Status_Error` | `#CF2121` | خطأ / إجراء خطر |
| `Status_Info` | `#106EBE` | معلومات |

### 2.2 Typography

المصدر الرسمي للخطوط والأنماط هو `Themes/Base/Typography.xaml`.

| Style Key | Size | Weight | الاستخدام |
| :--- | :--- | :--- | :--- |
| `Text_H1` | `28` | `ExtraBold` | عنوان الشاشة أو النافذة |
| `Text_H2` | `20` | `Bold` | عنوان قسم رئيسي أو عنوان side sheet |
| `Text_Section` | `15` | `SemiBold` | عنوان بطاقة أو قسم فرعي |
| `Text_Body` | `14` | `Medium` | النصوص الأساسية |
| `Text_Muted` | `12` | عادي | الملاحظات والوصف الخفيف |
| `Text_Caption` | `11.5` | `SemiBold` | البيانات الثانوية والعناوين الصغيرة |
| `InputLabel` | `12` | اشتقاق من `Text_Caption` | تسمية الحقول |
| `Text_Metric` | `24` | `Bold` | أرقام المؤشرات |

الخط الأساسي:

- `Font_Primary`: `Segoe UI, Dubai, Tahoma`
- `Font_Icons`: `Segoe Fluent Icons, Segoe MDL2 Assets`

### 2.3 Layout and Size Tokens

المصدر الرسمي: `Themes/Base/Layout.xaml`

| Token | Value | الاستخدام |
| :--- | :--- | :--- |
| `Size_Interactive_Height` | `30` | ارتفاع الأزرار والحقول القياسية |
| `Size_TitleBar_Height` | `30` | أزرار شريط العنوان |
| `Size_Header` | `56` | ترويسة التطبيق |
| `Size_RightRail_Width` | `232` | الشريط الجانبي الرئيسي |
| `Size_SideSheet_Width` | `360` | عرض الـ side sheet |
| `Space_04` / `08` / `12` / `16` / `20` / `24` / `32` | قيم مسافات قياسية | لا تستخدم قيمًا عشوائية بدلها |
| `Padding_Card` | `12` | الحشو القياسي للبطاقة |
| `Space_Padding_Button` | `16,0` | حشو الأزرار |
| `Space_Padding_Input` | `12,0` | حشو الحقول |
| `Space_Padding_SideSheet` | `16,16,16,20` | الحشو الداخلي للـ side sheet |
| `Corner_Shell` | `0` | الحاوية الخارجية للتطبيق |
| `Corner_Inner` | `10` | البطاقات والحقول والأزرار |
| `Space_Unified` | `8` | وحدة المسافة الأساسية |
| `Space_Section_Bottom` | `0,0,0,8` | الفاصل القياسي بين الأقسام |

### 2.4 Interaction and State Tokens

المصدر: `Themes/Base/DesignSystemV2.xaml`

| Resource Key | الاستخدام |
| :--- | :--- |
| `State_Focus_Border` | حدود التركيز |
| `State_Error_Border` | حدود الخطأ |
| `State_Disabled_Bg` | خلفية العنصر المعطل |
| `State_Disabled_Text` | نص العنصر المعطل |
| `Animation.FadeIn` | انتقالات الظهور الخفيفة |
| `Panel.EmptyState` | الحالة الفارغة |

---

## 3. Global App Defaults

هذه القواعد محمولة أصلًا من `App.xaml` ويجب عدم كسرها إلا لحاجة واضحة:

- كل `Window`:
  - `FlowDirection="RightToLeft"`
  - `FontFamily="{StaticResource Font_Primary}"`
  - `Background="{StaticResource Surface_Bg_Light}"`
- كل `UserControl`:
  - RTL
  - `Font_Primary`
- كل `Control`:
  - `Font_Primary`
  - RTL
- كل `TextBlock` الخام:
  - لون افتراضي مناسب
  - `TextWrapping="Wrap"`

### قواعد إلزامية

- لا تستخدم `FontFamily` محليًا إلا عند الحاجة الأيقونية.
- لا تستخدم `Background="#..."` أو `BorderBrush="#..."` مباشرة إذا كان هناك token مناسب.
- لا تستخدم `CornerRadius` محليًا بقيمة جديدة إذا كان `Corner_Inner` أو `Corner_Shell` كافيًا.
- لا تعتمد على `TextBlock` الخام للعناوين؛ استخدم دائمًا `Text_H1`, `Text_H2`, `Text_Section`, `Text_Body`, `Text_Muted`, `Text_Caption`.

---

## 4. Page Archetypes

### 4.1 Main Workspace Pages

هذا النمط يطبق على الشاشات الرئيسية مثل `TodayDeskView`, `DataTableView`, `OperationCenterView`, `SettingsView`.

#### البنية القياسية
1. شريط مؤشرات علوي عند الحاجة باستخدام `MetricStrip.Panel`.
2. بطاقة محتوى رئيسية باستخدام `Card.Panel`.
3. مرشحات أو أفعال في أعلى البطاقة نفسها، لا في ترويسة منفصلة بلا داع.
4. جدول أو محتوى تفصيلي أسفل نفس البطاقة أو في بطاقة مستقلة تالية.

#### قواعد
- الخلفية الرمادية `Surface_Bg_Light` يجب أن تبقى مرئية بين البطاقات.
- لا تجمع الصفحة كلها داخل حاوية بيضاء واحدة كبيرة.
- الفاصل القياسي بين الأقسام هو `8px`، ويمكن توسيعه إلى `16-24px` بين الكتل الكبرى فقط.
- إذا احتاجت الصفحة إلى مؤشرات، استخدم `MetricStrip.Panel` بدل اختراع شريط جديد.

### 4.2 Detail File Pages

هذا النمط يطبق على صفحات مثل `GuaranteeFileView`.

#### البنية القياسية
- بطاقة علوية رئيسية لعنوان الملف والخلاصة.
- بطاقات مستقلة للأفعال أو الاستعلامات أو الطلبات المتاحة.
- بطاقات مستقلة للطلبات المرتبطة، التسلسل الزمني، المرفقات، أو أي كتل كثيفة.

#### قواعد
- عنوان الصفحة يجب أن يكون داخل أول بطاقة رئيسية، لا في رأس عائم منفصل بلا محتوى.
- الإجراءات المتكررة يجب أن تظهر كبطاقات أو أزرار ضمن بنية الصفحة، لا كسلسلة غير منظمة من الأزرار.
- الأقسام التحليلية والتاريخية يجب أن تكون واضحة بصريًا، لا مجرد نصوص متتابعة.

### 4.3 Dialog Windows

هذا النمط يطبق على نوافذ مثل:
`CreateExtensionRequestWindow`, `CreateReductionRequestWindow`, `CreateReleaseRequestWindow`, `CreateLiquidationRequestWindow`, `CreateVerificationRequestWindow`, `CreateReplacementRequestWindow`, `RecordWorkflowResponseWindow`, `AttachmentListWindow`, `TextPromptWindow`.

#### البنية القياسية
1. خلفية النافذة: `Surface_Bg_Light`.
2. بطاقة عنوان أو لوحة إرشادية أعلى النافذة.
3. بطاقة محتوى رئيسية واحدة على الأقل.
4. شريط إجراءات سفلي أفقي.

#### قواعد
- الإجراء الأساسي يجب أن يستخدم `Button.Primary`.
- الإجراء الثانوي أو الإلغاء يجب أن يستخدم `Button.Subtle`.
- ترتيب الأزرار يجب أن يكون واضحًا وثابتًا.
- في النوافذ التشغيلية، يجب أن يفهم المستخدم الهدف خلال أول كتلة مرئية بدون قراءة الصفحة كاملة.
- الحقول متعددة الأسطر مسموحة بارتفاع أكبر من `Size_Interactive_Height` عند الحاجة.

### 4.4 Side Sheets

هذا النمط يطبق على `GuaranteeSideSheetView` و`RequestSideSheetView`.

#### البنية القياسية
- عرض ثابت مستند إلى `Size_SideSheet_Width`.
- حشو داخلي متوافق مع `Space_Padding_SideSheet`.
- عنوان `Text_H2` مع سطر وصف `Text_Muted`.
- صف بطاقات `Card.Compact` للمؤشرات السريعة.
- بطاقة أو أكثر من `Card.Panel` للتفاصيل.

#### قواعد
- الـ side sheet ليس صفحة كاملة مصغرة، بل ملخص عملي سريع.
- تجنب التراص الطويل للنصوص بدون تقسيم.
- لا تكدس أكثر من إجراء أساسي واحد في نفس المقطع.

### 4.5 History and Insight Windows

هذا النمط يطبق على `GuaranteeHistoryWindow` و`InquiryResultWindow`.

#### القواعد
- بطاقة عنوان واضحة.
- بطاقات مؤشرات أو ملخصات صغيرة عند الحاجة.
- بطاقة رئيسية لجدول أو نتيجة أو خط زمني.
- بطاقة منفصلة للأفعال إذا كانت الإجراءات متعددة.
- النتائج التحليلية يجب أن تشرح نفسها بصريًا: عنوان، جواب مختصر، أدلة، جدول أو خط زمني.

### 4.6 Settings and Utility Pages

ينطبق على `SettingsView` وما يشبهها.

#### القواعد
- كل مجموعة وظيفية في بطاقة مستقلة.
- لا يُسمح بصفحات إعدادات طويلة جدًا داخل بطاقة واحدة عمودية.
- النصوص الوصفية يجب أن تكون `Text_Body` أو `Text_Muted` حسب الأهمية.

---

## 5. Component Standards

### 5.1 Cards

المصدر الرسمي: `Themes/Controls/Cards.xaml`

- `Card.Panel`: الحاوية القياسية لكل قسم رئيسي.
- `Card.Compact`: المؤشرات والبلاطات الصغيرة.
- `MetricStrip.Panel`: شريط المؤشرات السريع.

#### قواعد
- أي محتوى مستقل وظيفيًا يجب أن يكون داخل بطاقة.
- البطاقات التفاعلية يمكن أن تستخدم حدود hover باللون الأخضر.
- اللوحات الإرشادية أو feedback panels يجب أن تُشتق من `Card.Panel` مع ضبط الخلفية/الحدود فقط.

### 5.2 Buttons

المصدر: `Themes/Controls/Buttons.xaml`

- `Button.Primary`: الإجراء الأساسي في المقطع.
- `Button.Subtle`: الإجراء الثانوي الافتراضي.
- `Button.Ghost`: استخدام محدود للحالات الخفيفة.
- `Button.Nav` و`Button.Nav.V2`: التنقل الرئيسي.
- `Button.TitleBar`: أزرار شريط العنوان.

#### قواعد
- لا تضع أكثر من زر أساسي واحد داخل نفس الكتلة إلا عند ضرورة قوية جدًا.
- لا تبتكر أشكال أزرار جديدة داخل الشاشات.
- تأثير الضغط `Push` موجود في القالب؛ لا تضف بديلًا محليًا متعارضًا.

### 5.3 Inputs

المصدر: `Themes/Controls/Inputs.xaml`

- `TextBox`, `ComboBox`, `DatePicker` لها قوالب موحدة.
- استخدم `InputLabel` لعناوين الحقول.
- الارتفاع الافتراضي للحقول هو `30`.

#### قواعد
- لا تستخدم `Height` عشوائي للحقول القياسية.
- الحقول متعددة الأسطر فقط هي التي تكسر الارتفاع القياسي.
- صفوف اختيار الملفات تكون عادة: `TextBox` للعرض + `Button.Subtle` للتصفح.
- لا تلوّن الحقول يدويًا في حالات التركيز أو الخطأ؛ اعتمد على الـ tokens.

### 5.4 DataGrid Standards

#### القواعد الأساسية
- يفضل `DataGridTextColumn` عندما يكون المحتوى نصيًا مباشرًا.
- يجب تحديد `MinWidth` لكل عمود.
- المحاذاة القياسية:
  - نص عربي: `DataGridText.Rtl`
  - رقم/عملة: `DataGridText.Ltr`
  - تاريخ/حالة قصيرة/تسلسل: `DataGridText.Center`
- استخدم `DataGridTemplateColumn` فقط عندما تحتاج تركيبًا بصريًا فعليًا، لا لمجرد عرض نص عادي.

#### استقرار التخطيط
- أي واجهة تحتوي جدولًا قد يحتاج إلى تصحيح عند الظهور إذا كانت تُحمّل وهي مخفية.
- النمط المعتمد:

```csharp
private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
{
    if ((bool)e.NewValue)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            grid.UpdateLayout();
            foreach (var col in grid.Columns)
            {
                var width = col.Width;
                col.Width = 0;
                col.Width = width;
            }
        }), DispatcherPriority.Loaded);
    }
}
```

### 5.5 Lists, Attachments, and Timelines

#### القواعد
- القوائم البسيطة تستخدم `ListBox.Flat` أو عناصر مبنية على `Card.Compact`.
- المرفقات والعناصر الزمنية يمكن أن تستخدم بطاقات صغيرة بحدود خفيفة.
- لا تترك العناصر كصفوف نصية خام إذا كانت تمثل كيانًا يمكن التفاعل معه.

### 5.6 Empty States

- لا يجوز ترك بطاقة فارغة أو شاشة فارغة دون تفسير.
- استخدم `Panel.EmptyState` مع أيقونة ووصف واضح.
- الحالة الفارغة يجب أن تقول للمستخدم: ما الذي لا يوجد، ولماذا قد لا يكون موجودًا، وما الإجراء التالي إن وجد.

---

## 6. RTL, Text Alignment, and Content Direction

### 6.1 RTL First
- كل الواجهات العربية تعمل RTL.
- لا يُعكس هذا إلا في أجزاء تقنية محدودة مثل بعض عناصر التاريخ أو popups الداخلية التي يتطلبها القالب.

### 6.2 Text Alignment
- في النصوص القياسية، اعتمد على أنماط typography الجاهزة بدل ضبط `TextAlignment` يدويًا.
- العناوين والنصوص ذات الأنماط الرسمية (`Text_H1`, `Text_H2`, `Text_Section`, `Text_Body`, `Text_Muted`, `Text_Caption`) هي المرجع الأول.
- عند الحاجة إلى محاذاة خاصة، يجب أن تكون لسبب واضح مثل أرقام أو ختم زمني أو قيمة نقدية.

### 6.3 Numeric and Date Content
- القيم النقدية والأرقام تعتمد `DataGridText.Ltr` داخل الجداول.
- التواريخ المختصرة والحالات الصغيرة تعتمد `DataGridText.Center`.
- لا يتم خلط محاذاة النص العربي مع القيم الرقمية داخل نفس النمط بلا حاجة.

---

## 7. Motion, Feedback, and Interaction

### 7.1 Hover and Selection
- hover على العناصر التفاعلية يجب أن يظهر حدودًا أو خلفية خفيفة متسقة مع `Brand_Emerald` و`Surface_Active`.
- التحديد في القوائم والجداول يستخدم `Brand_Emerald_12`.

### 7.2 Pressed State
- الأزرار الأساسية والثانوية تعتمد تأثير الضغط الموجود في القوالب.
- لا تضف محليًا تأثيرًا مختلفًا يناقض القالب العام.

### 7.3 Focus and Validation
- التركيز يستخدم `State_Focus_Border`.
- الخطأ يستخدم `State_Error_Border`.
- التعطيل يعتمد `State_Disabled_Bg` و`State_Disabled_Text`.

### 7.4 Motion
- `Animation.FadeIn` هو الانتقال المعتمد لظهور الصفحات أو الكتل الجديدة عندما تكون الحركة مناسبة.
- الحركة يجب أن تبقى خفيفة وخادمة للفهم، لا للزخرفة.
- لا يسمح بتكديس عدة انتقالات متنافسة داخل نفس الشاشة.

---

## 8. Navigation Architecture

### 8.1 Main Navigation
- عناصر التنقل الرئيسية تستخدم `Button.Nav` أو `Button.Nav.V2`.
- الحالة النشطة تدار عبر `Nav.IsActive`.
- لا يعتمد التنقل على `Tag` فقط كوسيلة بصرية.

### 8.2 Warm-Up Policy
- مساحات العمل الرئيسية يمكن تهيئتها أثناء الخمول باستخدام `DispatcherPriority.ApplicationIdle` لتحسين أول انتقال.
- هذا تحسين أداء مسموح به ويفضل للشاشات الثقيلة.

---

## 9. Compliance Checklist

أي شاشة جديدة أو شاشة مُعدلة يجب أن تجتاز القائمة التالية:

- تستخدم tokens الرسمية للألوان، لا قيمًا صلبة جديدة.
- تستخدم typography styles الرسمية للعناوين والنصوص.
- تلتزم RTL على مستوى الشاشة.
- تبني المحتوى داخل `Card.Panel` أو `Card.Compact` أو `MetricStrip.Panel` بحسب الحالة.
- لا تضع الصفحة كلها داخل كتلة بيضاء واحدة إذا كانت بنية البطاقات الأنسب.
- تستخدم `Button.Primary` للإجراء الأساسي و`Button.Subtle` للإجراءات الثانوية.
- تستخدم `InputLabel` والحقول الموحدة للنماذج.
- تطبق معايير `DataGrid` من حيث نوع الأعمدة والمحاذاة والحدود الدنيا.
- تعرض حالة فارغة واضحة عند غياب المحتوى.
- لا تعرّف أسلوبًا بصريًا محليًا جديدًا إلا عند غياب بديل عام واضح.

---

## 10. File References

- الألوان والخلفيات: `Themes/Base/Brushes.xaml`
- الخطوط والنصوص: `Themes/Base/Typography.xaml`
- المسافات والأحجام والزوايا: `Themes/Base/Layout.xaml`
- الأزرار: `Themes/Controls/Buttons.xaml`
- الحقول: `Themes/Controls/Inputs.xaml`
- البطاقات والجداول وأنماط القوائم: `Themes/Controls/Cards.xaml`
- التحسينات والحالات والتأثيرات: `Themes/Base/DesignSystemV2.xaml`
- القواعد العامة المحملة على مستوى التطبيق: `App.xaml`

---

## 11. Final Rule

هذا الملف لم يعد "مرجعًا للواجهات الرئيسية فقط".
من هذه اللحظة، هو المرجع التصميمي الكامل لكل شاشة في البرنامج.

أي تباين قائم في الشاشات القديمة يُعامل كعمل ترحيل مطلوب، لا كسياسة تصميم بديلة.
