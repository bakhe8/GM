# AI Handoff

**Updated:** 2026-05-03

This is the short continuation point for a new AI/Codex chat. It intentionally avoids the old transition archive.

## Current Branch

- Branch: `feature/v1.1-operational-polish`
- App target: `v1.1.0-rc.5`
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
- Request work is handled from the guarantee timeline, daily follow-up, quick actions, and reports.
- The old operational inquiry/context action layer was removed. Do not reintroduce it without an explicit product decision.
- The beneficiary is fixed for all guarantees: `مستشفى الملك فيصل التخصصي ومركز الأبحاث`.
- The supplier is the variable party shown to the user in lists, detail panels, filters, and reports.

## Workflow Rules

- Annulment/reversal is removed as an operational workflow.
- Expired guarantees allow release/return only.
- Extension and reduction may happen sequentially while the guarantee is active and not expired.
- Release, liquidation, and replacement are terminal after bank confirmation and cancel other pending requests on the same root.
- Replacement creates a new guarantee and marks the old root as replaced.
- Guarantee edit is administrative only: supplier/beneficiary display, notes, and attachments; not amount, expiry, bank, guarantee number, type, or reference.
- The administrative edit guard is enforced in `GuaranteeRepository.UpdateGuaranteeWithAttachments`, not only in the UI.
- The timeline is the source of truth for the guarantee lifecycle.

## Verification Baseline

- `dotnet build GuaranteeManager.csproj`
- Result: `0 warnings / 0 errors`.
- `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj`
- Result: `167/167`.
- `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release`
- Result: `167/167`.
- `.\scripts\publish_release.ps1`
- Result: builds, runs Release tests, publishes `v1.1.0-rc.5`, and creates the local zip package.

## Primary References

- `README.md`
- `Doc/CURRENT_STATE.md`
- `Doc/README.md`
- `Doc/git_workflow.md`
- `Doc/releases/README_v1.1.0-rc.5.md`
- `Doc/guides/Workflow_Event_Logic_Study.md`
- `Doc/guides/User_Guide_Final.md`
- `Doc/guides/Next_Development_Plan.md`
- `Doc/guides/Repository_Stabilization_Checklist_2026-04-30.md`
- `Doc/design/Visual_Identity.md`
- `Doc/audits/Missing_Features_Report.md`

## Command To Start A New Chat

> اقرأ `AI_HANDOFF.md` و`Doc/CURRENT_STATE.md` وراجع `git status --short --branch` ثم تابع من آخر نقطة.
