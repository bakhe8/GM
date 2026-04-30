# AI Handoff

**Updated:** 2026-05-01

This is the short continuation point for a new AI/Codex chat. It intentionally avoids the old transition archive.

## Current Branch

- Branch: `feature/v1.1-operational-polish`
- App target: `v1.1.0-rc.1`
- Always begin with `git status --short --branch`.

## Current Product Shape

- The active UI lives under `Presentation/`.
- Daily work is centered on:
  - `اليوم`
  - `الضمانات`
  - `البنوك`
  - `التقارير`
  - `الإعدادات`
- There is no standalone `Requests`, `Notifications`, or `Guarantee File` workspace in the active product.
- Request work is handled from the guarantee timeline, daily follow-up, operational inquiries, and reports.
- The beneficiary is fixed for all guarantees: `مستشفى الملك فيصل التخصصي ومركز الأبحاث`.
- The supplier is the variable party shown to the user in lists, detail panels, filters, and reports.

## Workflow Rules

- Annulment/reversal is removed as an operational workflow.
- Expired guarantees allow release/return only.
- Extension and reduction may happen sequentially while the guarantee is active and not expired.
- Release, liquidation, and replacement are terminal after bank confirmation and cancel other pending requests on the same root.
- Replacement creates a new guarantee and marks the old root as replaced.
- Guarantee edit is administrative only: supplier/beneficiary display, notes, and attachments; not amount, expiry, bank, guarantee number, type, or reference.
- The timeline is the source of truth for the guarantee lifecycle.

## Verification Baseline

- Last focused test run: `dotnet test .\GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore -p:BaseOutputPath=.\artifacts\rc1-version-test\`
- Result: `107/107`.
- Debug build passes: `dotnet build .\my_work.sln -c Debug --no-restore`.
- Release package build passes through `.\scripts\publish_release.ps1`, including Release tests `105/105`.
- Isolated package UAT passed from the `v1.1.0-preview.4` build and is the evidence used to promote this tree to `v1.1.0-rc.1`.

## Primary References

- `Doc/CURRENT_STATE.md`
- `missing_features_report.md`
- `Doc/guides/Workflow_Event_Logic_Study.md`
- `Doc/guides/User_Guide_Final.md`
- `Doc/guides/Next_Development_Plan.md`
- `Doc/guides/Repository_Stabilization_Checklist_2026-04-30.md`

## Command To Start A New Chat

> اقرأ `AI_HANDOFF.md` و`Doc/CURRENT_STATE.md` وراجع `git status --short --branch` ثم تابع من آخر نقطة.
