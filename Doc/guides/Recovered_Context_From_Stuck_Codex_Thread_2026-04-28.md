# استرجاع سياق المحادثة العالقة

هذا الملف يستعيد الجزء الأخير المهم من محادثة Codex التي تعطلت في VS Code ولم تعد تفتح بشكل طبيعي.

> **ملاحظة بعد الاسترجاع — 2026-04-28**
>
> هذا الملف يبقى snapshot تاريخيًا للمحادثة العالقة، وليس مرجع الأولوية الأحدث.
> المرحلة التي كان يوصي بها في نهايته، وهي إعادة تأليف وسط `Guarantee File`
> حول `الطلبات / الخط الزمني / المخرجات / المرفقات`، أصبحت الآن WIP منفذًا في الشجرة الحالية.
> مرجع القرار التنفيذي الأحدث هو:
>
> - [UI_Recovery_Priority_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_Recovery_Priority_Map.md>)

## مصدر الاسترجاع

- ملف الجلسة:
  - `C:\Users\Bakheet\.codex\sessions\2026\04\25\rollout-2026-04-25T12-09-11-019dc3e6-6c38-72d2-a55c-d747847b7a9d.jsonl`
- عنوان المحادثة:
  - `شغّل البرنامج`
- آخر commit ذُكر صراحة في نهاية الجزء المستعاد:
  - `bea1af9` — `feat: clarify guarantee file entry and file map`

## لماذا نحتاج هذا الملف

المحادثة الأصلية أصبحت ضخمة جدًا على واجهة VS Code، لذلك صار فتحها يعرض شاشة فارغة ويعلق زر الإرسال.
لكن محتوى الجلسة نفسه ما زال موجودًا، وهذا الملف يلخص آخر ما أنجزناه حتى نستطيع مواصلة العمل دون فقدان الاتجاه.

## أين توقفنا فعليًا

كنا نعمل على مسار **إعادة تشكيل واجهات النظام تشغيليًا** بحيث تصبح:

- مبنية على بيوت عمل حقيقية
- واضحة هرميًا
- أقل تشتيتًا
- وأكثر اتساقًا بين السايدبار والسطوح الداخلية

وكانت آخر مرحلة مركزة على:

- `Shell / Sidebar`
- `Today`
- `Requests`
- `Guarantee File`

## القرارات المعمارية الرئيسية التي اتفقنا عليها

### 1. قبل أي إصلاحات، ثبتنا خريطة النظام كاملة

تم إنشاء/اعتماد هذه المراجع:

- [UI_System_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_System_Map.md>)
- [UI_Component_Atlas.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_Component_Atlas.md>)
- [UI_Recovery_Priority_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_Recovery_Priority_Map.md>)
- [Workflow_First_UI_Recovery_Plan.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Workflow_First_UI_Recovery_Plan.md>)
- [CURRENT_STATE.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md>)

المقصود من هذا القرار:

- لا نصلح شاشة ثم نكتشف لاحقًا أن القرار عارض أو متناقض مع بقية النظام
- كل Surface له مكان واضح ضمن خريطة البرنامج
- كل مكوّن داخل كل واجهة له دور مقصود

### 2. ترتيب الأولويات أصبح Workflow-first

الترتيب المرجعي الذي استقرينا عليه:

- Tier 1
  - `Shell / Navigation / Sidebar`
  - `Today`
  - `Requests`
  - `Guarantee File`
- Tier 2
  - `Guarantees`
  - `Notifications` كعدسة انتقالية
  - `HistoryDialog`
  - `OperationalInquiryDialog`
- Tier 3
  - `Reports`
  - `Banks`
- Tier 4
  - `Settings`

### 3. السايدبار العليا لم تعد قائمة شاشات مسطحة

تم تحويلها إلى بيوت عمل أوضح، وانتهينا إلى أن العناصر العليا الأساسية هي:

- `اليوم`
- `الطلبات`
- `الضمانات`
- `التحليلات`
- `الإعدادات`

وكان هذا ناتجًا عن قرارين مهمين:

- `التنبيهات` لم تعد عنصرًا علويًا مستقلاً
  - صارت **عدسة داخل `اليوم`**
- `البنوك` لم تعد عنصرًا علويًا مستقلاً
  - صارت **عدسة داخل `التقارير/التحليلات`**

### 4. `التقارير` أعيد تعريفها كبيت أوسع

توقفنا عن اعتبارها Surface تقارير ضيقة فقط، وصارت:

- `التحليلات والمخرجات`

وشمل ذلك:

- تغيير الاسم الظاهر في السايدبار
- تحديث aliases التنقل والبحث
- جعل `عدسة البنوك` عدسة انتقالية داخل هذا البيت

### 5. `Requests` أعيد تقديمها كـ Queue يومي

ما ثبتناه هناك:

- الاسم والمنطق أصبحا أقرب إلى `الطلبات اليومية`
- أُضيف header يشرح دورها
- زر الـ toolbar الرئيسي أصبح **ديناميكيًا**
  - يعكس `الخطوة التالية` للطلب المحدد
- اللوحة اليمنى صارت تقرأ هرميًا:
  - `الخطوة التالية`
  - `فتح ملف الضمان`
  - `فتح الخطاب`
- الإنشاء والتصدير أصبحا أدوات ثانوية، لا محور الواجهة

### 6. `Today` تحولت من سطح يومي عام إلى بيت متابعة حقيقي

داخل عدسة `متابعات الانتهاء` تحديدًا:

- لم نعد نعرض `top 8` فقط
- أصبح العرض يشمل **كل العناصر المطابقة**
- المقياس الرابع لم يعد `قريب الانتهاء` فقط
  - بل صار `متابعات الانتهاء`
  - ويجمع القريب من الانتهاء + المنتهي الذي يحتاج متابعة

ثم طورنا هذه العدسة داخليًا:

- بطاقات الـ KPI أصبحت سياقية داخل هذه العدسة
  - `قريب الانتهاء`
  - `منتهي`
  - `إجمالي القيمة`
  - `أقرب تاريخ`
- رؤوس الجدول أصبحت خاصة بهذه العدسة
  - `المستوى`
  - `الأيام`
  - `تاريخ الانتهاء`
- الصفوف نفسها صارت تعرض:
  - شدة المتابعة
  - كم بقي أو كم تأخر
  - تاريخ الانتهاء
- detail panel صارت تستخدم لغة تشغيلية مناسبة:
  - `نوع المتابعة`
  - `المستوى`
  - `المدة`
  - `المسار`
  - `الإجراء المقترح`
  - `ملاحظة المتابعة`

### 7. `Guarantee File` بدأنا إصلاحه على دفعتين واضحتين

#### الدفعة الأولى

قسم `الإجراءات المتاحة الآن` لم يعد شبكة واحدة مسطحة، بل صار مقسمًا إلى:

- `نفّذ الآن`
- `راجع وافتح`
- `أفعال متقدمة`

وكان الهدف:

- عدم حذف أي منطق
- لكن إعادة قراءة نفس الأفعال بهرمية أوضح

#### الدفعة الثانية وهي آخر ما أُنجز قبل تعطل المحادثة

هذه كانت آخر خطوة مكتملة بوضوح:

- نقل `ActionSummary` إلى بداية الملف تحت عنوان:
  - `ابدأ من هنا`
- تحويل `تنقل داخل الملف` إلى:
  - `خريطة الملف`
- تقسيم خريطة الملف إلى بيتين:
  - `القرار والتنفيذ`
  - `المراجعة والأثر`

الفكرة التي استقررنا عليها هنا كانت:

- أعلى `Guarantee File` يجب أن يجيب أولًا:
  - `ما القرار الآن؟`
- وبعدها فقط:
  - `إلى أي قسم أذهب؟`

## الملفات التي ذُكرت صراحة في آخر جزء من المحادثة

### مراجع ووثائق

- [UI_System_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_System_Map.md>)
- [UI_Component_Atlas.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_Component_Atlas.md>)
- [UI_Recovery_Priority_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/UI_Recovery_Priority_Map.md>)
- [Workflow_First_UI_Recovery_Plan.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Workflow_First_UI_Recovery_Plan.md>)
- [CURRENT_STATE.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/CURRENT_STATE.md>)
- [Surface_Audit_Matrix.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Surface_Audit_Matrix.md>)
- [Navigation_Architecture_Map.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/guides/Navigation_Architecture_Map.md>)
- [Doc/README.md](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/README.md>)

### ملفات واجهات ومنطق

- [MainWindow.xaml](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Shell/MainWindow.xaml>)
- [ShellViewModel.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Shell/ShellViewModel.cs>)
- [ShellWorkspaceFactory.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Shell/ShellWorkspaceFactory.cs>)
- [ShellWorkspaceAliasResolver.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Shell/ShellWorkspaceAliasResolver.cs>)
- [DashboardWorkspaceDataService.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Dashboard/DashboardWorkspaceDataService.cs>)
- [DashboardWorkspaceSurface.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Dashboard/DashboardWorkspaceSurface.cs>)
- [ReportsWorkspaceSurface.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Reports/ReportsWorkspaceSurface.cs>)
- [RequestsWorkspaceSurface.cs](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Requests/RequestsWorkspaceSurface.cs>)
- [GuaranteeDetailPanel.xaml](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Guarantees/GuaranteeDetailPanel.xaml>)
- [Navigation.xaml](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Themes/Navigation.xaml>)

## آخر نتيجة تنفيذية مؤكدة

في نهاية المحادثة كان آخر تقرير تنفيذي يقول:

- تم تعديل أعلى `Guarantee File`
- تم تحديث `UI_Component_Atlas.md`
- تم تحديث `CURRENT_STATE.md`
- التحقق نجح:
  - `dotnet build .\\my_work.sln`
  - `dotnet test .\\my_work.sln --no-build` → `54/54`
- أُغلق العمل في commit:
  - `bea1af9`
- والشجرة كانت نظيفة تمامًا وقتها

## ما الذي كان متبقيًا بالضبط

آخر جملة مهمة جدًا قبل تعطل المحادثة كانت:

> الخطوة التالية الصحيحة: نكمل داخل `Guarantee File` لكن على **خريطة الأقسام الوسطى نفسها** حتى تتماشى قراءة `الطلبات / الخط الزمني / المخرجات / المرفقات` مع هذا المدخل الأوضح.

إذًا التبقي الفعلي لم يكن العودة إلى البداية، بل:

### المرحلة التالية المقصودة

إكمال `Guarantee File` من الوسط، خصوصًا:

- `الطلبات`
- `الخط الزمني`
- `المخرجات`
- `المرفقات`

بحيث تصبح هذه الأقسام:

- منسجمة مع المدخل الجديد `ابدأ من هنا`
- مرتبة حسب القرار ثم المراجعة ثم الأثر
- أقل تسطحًا
- وأكثر تشغيلية

## ماذا يعني هذا لنا الآن

نحن **لم نفقد الاتجاه** بالكامل.
الاتجاه المستعاد بوضوح هو:

1. الخريطة الكاملة للنظام موجودة
2. السايدبار العليا أعيد تنظيمها
3. `Today` عولجت تشغيليًا
4. `Requests` عولجت تشغيليًا
5. `Guarantee File` بدأنا فيه فعليًا
6. آخر نقطة توقف حقيقية كانت:
   - استكمال الأقسام الوسطى داخل `Guarantee File`

## التوصية العملية للعودة للعمل

إذا سنواصل من نفس المسار، فالبداية الصحيحة الآن هي:

1. فتح [GuaranteeDetailPanel.xaml](</C:/Users/Bakheet/Documents/Projects/Work/my_work/Presentation/Views/Guarantees/GuaranteeDetailPanel.xaml>)
2. مراجعة التكوين الحالي في أعلى الملف
3. ثم إعادة ترتيب الأقسام الوسطى التالية:
   - الطلبات
   - الخط الزمني
   - المخرجات
   - المرفقات
4. مع تحديث:
   - `UI_Component_Atlas.md`
   - `CURRENT_STATE.md`
5. ثم:
   - `dotnet build .\\my_work.sln`
   - `dotnet test .\\my_work.sln --no-build`
