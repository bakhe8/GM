# Supported UI Automation API

هذا الملف يحدد **الواجهة العامة المعتمدة** لأداة UI Automation داخل المشروع.

المرجع التنفيذي داخل الكود هو:

- [Get-UiSupportedApi](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/UIAutomation.Acceptance.psm1:1)

## القاعدة العامة

- ما يظهر عبر:
  - `Get-Command -Module UIAutomation.Acceptance`
  - أو `Get-UiSupportedApi`
  يعتبر **API عامة معتمدة**.
- ما عدا ذلك داخل:
  - `scripts/modules/*.ps1`
  يعتبر **تفصيلًا داخليًا** قابلًا للتغيير أثناء إعادة الهيكلة.

## متى أستخدم ماذا؟

### 1. الاستخدام اليومي

ابدأ عادةً من:

- [ui_explore.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/ui_explore.ps1:1)
- [run_ui_acceptance.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_acceptance.ps1:1)
- [run_ui_tooling_regression.ps1](c:/Users/Bakheet/Documents/Projects/Work/my_work/scripts/run_ui_tooling_regression.ps1:1)

هذه هي نقاط الدخول الرسمية لمعظم الجولات.

### 2. الاستخدام البرمجي المباشر

استخدم أوامر module العامة فقط عندما نحتاج:

- بناء harness جديد
- regression suite جديدة
- قياس أو تشخيص أدق
- التفاعل مع عنصر أو نافذة خارج السيناريوهات الجاهزة

## الفئات العامة الحالية

### Session

- `Get-UiAcceptanceRepoRoot`
- `Get-UiAcceptanceArtifactsRoot`
- `Get-UiStorageRoot`
- `Start-UiTargetApplication`
- `Get-UiProcess`
- `Get-UiCapabilitySessionPath`
- `Get-UiCapabilitySessionState`
- `Start-UiCapabilitySession`
- `Stop-UiCapabilitySession`

### Diagnostics

- `Get-UiDiagnosticsPaths`
- `Get-UiTimelinePath`
- `Get-UiCalibrationPath`
- `Get-UiCalibrationProfile`
- `Get-UiTimelineEntries`
- `Get-UiPerformanceSummary`
- `Write-UiTimelineEvent`
- `Get-UiShellStateSnapshot`
- `Get-UiRecentEvents`
- `Get-UiSupportedApi`

### Windows

- `Get-UiTopLevelWindows`
- `Get-UiWindowsCatalog`
- `Resolve-UiWindow`
- `Show-UiWindow`
- `Wait-UiWindow`

### Elements

- `Wait-UiElement`
- `Find-UiElements`
- `Find-UiProcessElements`
- `Get-UiElementSummary`
- `Get-UiBounds`
- `Get-UiDescendants`
- `Invoke-UiElement`
- `Set-UiElementValue`
- `Get-UiEditNearLabel`
- `Get-UiButtonByText`
- `Get-UiSidebarButton`

### Dialogs

- `Get-UiActiveDialog`
- `Get-UiDialogActionButton`
- `Wait-UiElementReady`
- `Wait-UiWindowGone`
- `Invoke-UiDialogActionButton`

### Input And Navigation

- `Send-UiVirtualKey`
- `Send-UiSendKeys`
- `Invoke-UiSidebarNavigation`

### Mouse

- `Get-UiCursorPosition`
- `Move-UiMouse`
- `Invoke-UiMouseClick`
- `Invoke-UiMouseRightClick`
- `Invoke-UiMouseDoubleClick`
- `Invoke-UiMouseHover`
- `Invoke-UiMouseDrag`
- `Invoke-UiMouseScroll`

هذه الفئة تمنحنا الآن طبقة ماوس حرة عند الحاجة فقط:

- يمكن الاستهداف عبر:
  - `AutomationId`
  - `Name`
  - `Text`
  - `WindowTitle`
  - `WindowAutomationId`
- أو عبر `X/Y` مباشرة
- أو انطلاقًا من موضع المؤشر الحالي عندما لا نمرر selector

والمبدأ المقصود هنا مهم:

- **الماوس ليس وضعًا دائمًا**
- بل قدرة لحظية نستدعيها داخل نفس الاستكشاف الحر
- ثم نعود مباشرة إلى الوضع الخفيف

### Capture

- `Save-UiWindowScreenshot`
- `Save-UiDesktopScreenshot`
- `New-UiContactSheet`
- `Compare-UiImages`

### Media

- `Get-UiMediaSessionPath`
- `Get-UiMediaSessionState`
- `Get-UiMediaScopeView`
- `Get-UiAudioScopePolicy`
- `Get-UiMediaProviderCatalog`
- `Invoke-UiMediaBrokerSweep`
- `Start-UiVideoCaptureSidecar`
- `Stop-UiVideoCaptureSidecar`
- `Start-UiAudioCaptureSidecar`
- `Stop-UiAudioCaptureSidecar`

هذه الفئة لا تضيف “وسائط ثقيلة دائمًا”، بل تضيف **أساسًا مرنًا عند الطلب**:

- مزود فيديو يمكن تشغيله فقط عند الحاجة
- حالة session مستقلة وقابلة للقراءة
- scope attestation صريحة توضّح:
  - ما العملية المستهدفة
  - ما النافذة المقصودة
  - وما إذا كانت foreground أثناء الالتقاط مرتبطة بالبرنامج أم لا
- single-instance cleanup هادئ قبل أي start جديدة
- إيقاف واضح يعيد الأداة إلى وضعها الخفيف

ومهم أيضًا هنا:

- `Psr.ScreenTrace` هي أول sidecar فيديو متاحة الآن على هذه البيئة
- لكنها **ليست process-bound أو window-bound** مثل التقاط الصورة
- لذلك نعتمد الآن على:
  - `ScopeStatus`
  - `EvidenceIsolation`
  - `TrustedForReasoning`
  بدل الادعاء أن الفيديو معزول تلقائيًا
- `AudioCapture` ما زالت غير متاحة كمزود فعلي
- لكن عندنا الآن **Scoped Audio Contract** قبل المزود:
  - `Get-UiAudioScopePolicy`
  - تشرح أن السياسة المستهدفة هي:
    - `per-app-attested`
    - بدون `system-mix fallback`
  - وإذا طُلب `AudioOn` الآن فالأداة:
    - ترفض البدء بصراحة
    - وتسجل `audio-start-blocked`
    - وتوضح لماذا hearing evidence غير موثوقة بعد
- وقد تكون artifact الفيديو:
  - `saved`
  - أو `missing`
  حسب ما إذا كان المزود نفسه أخرج ملفًا محفوظًا أم لا

### Capabilities

- `Get-UiCapabilityDefinitions`
- `Get-UiCapabilityObservationPath`
- `Get-UiCapabilityObservationEntries`
- `Enable-UiCapability`
- `Disable-UiCapability`
- `Invoke-UiCapabilityBrokerSweep`

## ما الذي لا نعدّه API عامة؟

- الدوال الموجودة فقط داخل:
  - `UiAutomation.Actions.ps1`
  - `UiAutomation.Session.ps1`
  - `UiAutomation.Dialogs.ps1`
  - `UiAutomation.Diagnostics.ps1`
  - `UiAutomation.Core.ps1`
  عندما لا تُصدَّر صراحة من facade
- أي helper مخصص لإعادة الهيكلة الحالية
- أي behavior نعتمد فيه على side effects غير موثقة

## مبدأ الثبات

نعد بالحفاظ على:

1. أسماء أوامر module العامة
2. أوامر:
   - `ui_explore.ps1`
   - `run_ui_acceptance.ps1`
   - `run_ui_tooling_regression.ps1`
3. بنية المخرجات الأساسية للتشخيص والـ regression

أما التفاصيل الداخلية فيمكن أن تتغير إذا:

- صارت الأداة أوضح
- صارت أسرع
- أو صارت أسهل في الاختبار والصيانة
