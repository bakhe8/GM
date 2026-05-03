# Screenshot Artifacts

هذا المجلد مخصص لمخرجات التحقق البصري. بعد تنظيف الأرشيف لم تعد لقطات baselines القديمة متتبعة في git.

## القاعدة الحالية

- `UIAcceptance/latest/` مخرجات generated لآخر تشغيل.
- أي مجلد زمني تحت `UIAcceptance/20*` مخرج تشغيل مؤقت.
- لا نضيف لقطات قديمة أو مسارات انتقالية إلى git.

## تشغيل القبول البصري

```powershell
.\scripts\run_ui_acceptance.ps1 -Scenario All
```

## تشغيل regression للأداة

```powershell
.\scripts\run_ui_tooling_regression.ps1 -Suite All
```

## الاستكشاف اليدوي

```powershell
.\scripts\ui_explore.ps1 -Action Probe
.\scripts\ui_explore.ps1 -Action Diagnostics
.\scripts\ui_explore.ps1 -Action State
```
