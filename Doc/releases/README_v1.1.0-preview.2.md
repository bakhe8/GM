# GuaranteeManager v1.1.0-preview.2

هذه هي **النسخة التطويرية الحالية** من البرنامج بعد إقفال جولة
`repository stabilization` الأساسية، واعتماد baseline أنظف للبنية والتوثيق
وأداة القبول.

> **مهم:** هذه ليست نسخة توزيع نهائية، ولا تحل محل baseline المنشور `v1.0.0`.
> هي نسخة تطوير ومراجعة داخلية فقط.

## حالة هذه النسخة

- رقم النسخة داخل التطبيق: `v1.1.0-preview.2`
- نوع الإصدار: **Preview**
- الغرض: تثبيت baseline نظيفة بعد stabilization قبل الدخول إلى UAT أوسع
- اسم الحزمة المتوقع: `GuaranteeManager_v1.1.0-preview.2_win-x64.zip`
- مجلد النشر المتوقع: `releases/v1.1.0-preview.2`

## ما الذي تغيّر مقارنة بـ `preview.1`

- جرى تقسيم التحول الكبير إلى commits منطقية بدل دفعة واحدة غامضة
- تم تنظيف الجذر من artifacts المؤقتة
- تم تثبيت `Doc/` كمصدر الحقيقة الرسمي
- تم فصل `latest/` generated عن `baselines/` الرسمية في لقطات القبول
- تم نقل ملف المعايرة البشرية إلى مسار ثابت ومتتبع
- بقيت baseline القبول المعتمدة محفوظة هنا:
  - [UIAcceptance/baselines/2026-04-26-stabilization](c:/Users/Bakheet/Documents/Projects/Work/my_work/Doc/Assets/Documentation/Screenshots/UIAcceptance/baselines/2026-04-26-stabilization)

## طريقة التشغيل

1. فك ضغط `GuaranteeManager_v1.1.0-preview.2_win-x64.zip`
2. افتح المجلد الناتج `v1.1.0-preview.2`
3. شغّل `GuaranteeManager.exe`

## قواعد التعامل مع هذه النسخة

- لا تُعامل كحزمة إطلاق نهائي
- لا يُبنى عليها قرار اعتماد للمستخدمين النهائيين
- تستخدم هذه النسخة كـ baseline نظيفة لجولات UAT التالية
- أي مخرجات generated جديدة لا تُتتبع إلا إذا أصبحت baseline أو مرجعًا رسميًا
- يمكن توليد الحزمة عبر السكربت: `scripts/publish_release.ps1`

## الانتقال القادم

ننتقل من `v1.1.0-preview.2` إلى:

- `preview` لاحقة فقط إذا ظهرت دفعات تطويرية مؤثرة جديدة
- `v1.1.0-rc.1` فقط بعد:
  1. جولة UAT واقعية مستقرة
  2. عدم وجود مشاكل مانعة مفتوحة
  3. ثقة تشغيلية كافية على بيانات أثقل أو حقيقية
