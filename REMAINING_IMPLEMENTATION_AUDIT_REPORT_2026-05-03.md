# Remaining Implementation Audit Report

**Date:** 2026-05-03
**Project:** GuaranteeManager
**Purpose:** Consolidates only the audit-report items that still require implementation after the latest fixes. Fixed or superseded findings were intentionally omitted.

## Verification Baseline

- Debug tests: `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore` -> `167/167` passed.
- Release tests: `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release --no-restore` -> `167/167` passed.
- Release artifact: `releases\GuaranteeManager_v1.1.0-rc.5_win-x64.zip`.
- Source reports consolidated and removed from the active queue:
  - `06_UX_UI_ARABIC_USABILITY_AUDIT_REPORT_2026-05-03.md`
  - `Code_Architecture_Audit_Report_2026-05-03.md`
  - `Database_Data_Integrity_Audit_Report_2026-05-03.md`
  - `Performance_Scalability_Audit_Report_2026-05-03.md`
  - `Reporting_Export_Audit_Report_AR.md`
  - `Security_Reliability_Audit_Report_2026-05-03.md`
  - `TESTING_REGRESSION_AUDIT_REPORT.md`
  - `WORKFLOW_AUDIT_REPORT.md`

## Implementation Completion Addendum

**Completion date:** 2026-05-03
**Completed version:** `v1.1.0-rc.5`
**Branch:** `feature/v1.1-operational-polish`
**Final release decision:** `Approved for internal pilot`
**Score:** `8.3/10`

This addendum supersedes the `v1.1.0-rc.4` gate result below for current release decisions. The `rc.4` section is retained as historical evidence of the blocker list that drove the implementation plan.

### Completed Fixes

| Area | Result |
|---|---|
| Release baseline hygiene | Version raised to `v1.1.0-rc.5`; release notes and active handoff docs now reference `167/167` tests and the `rc.5` artifact. `.gitignore` no longer hides source folders named `Attachments` or `Workflow`. |
| Reports performance | Report execution moved behind an async coordinator path with run locking and `قيد الإنشاء` UI state, preventing repeated report launches while work is in progress. |
| Durable file recovery | Added persistent pending file operation replay for attachment promotion, workflow response promotion, and cleanup failures during runtime initialization. |
| Backup resiliency | Added tests for locked manual-backup destination and invalid portable package restore preserving the current database. |
| Large-data guards | Added SQL `LIMIT/OFFSET` regression and a `5,000` guarantee workspace guard proving only the visible page is requested. |

### Final Command Evidence

| Check | Result | Evidence |
|---|---|---|
| `git status --short --branch` | Not clean | Branch is `feature/v1.1-operational-polish`; working tree still has many uncommitted/untracked repository-reorganization changes. |
| `git log --oneline -10` | Collected | Latest commit shown: `181ffed Use app chrome for dialog windows`. |
| `dotnet build GuaranteeManager.csproj --no-restore` | Pass | `0 Warning(s) / 0 Error(s)`. |
| `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore` | Pass | `167/167` passed, `0` skipped. |
| `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release --no-restore` | Pass | `167/167` passed, `0` skipped. |
| `.\scripts\publish_release.ps1` | Pass | Built `v1.1.0-rc.5`, reran Release tests `167/167`, published folder and zip artifact. |

Release artifact:

- Publish directory: `releases\v1.1.0-rc.5`
- Archive: `releases\GuaranteeManager_v1.1.0-rc.5_win-x64.zip`
- Artifact README included: `Yes`

### Current Gate Result

| Gate | Status | Evidence | Blocking Issues |
|---|---|---|---|
| Build & Tests | Pass | Build, Debug tests, Release tests, and publish script passed; no skipped tests. | None for internal pilot. |
| Data Safety | Pass | Versioning/history protections, hard-delete guards, transactional workflow paths, backup restore checks, and durable file operation replay are covered. | None for internal pilot. |
| Business Rules | Pass with concerns | Core workflow and administrative edit guard coverage remains green. | Continue UAT on real edge cases before wider rollout. |
| Performance | Pass with concerns | Main workspace pagination is guarded; reports now run in background; `5,000` row UI guard exists. | Measure real report duration during pilot. |
| UX | Pass with concerns | Arabic/RTL flows and running-report state are covered by code/tests; Settings exposes backup/restore paths. | Fresh packaged UI acceptance run still recommended before expansion. |
| Operational Readiness | Pass with concerns | Reproducible `rc.5` artifact exists with release notes and logs available. | Commit/tag the exact source baseline before limited/broad production. |

### Remaining Constraints Before Expansion

| Issue | Risk | Fix |
|---|---|---|
| Working tree not committed or tagged | The artifact is locally reproducible now, but not yet recoverable from a clean source-control tag. | Stage, review, commit, rebuild from clean checkout, and tag `v1.1.0-rc.5` or final `v1.1.0`. |
| Packaged UI acceptance not rerun | Visual/RTL regressions may be missed outside unit tests. | Run focused UI smoke/UAT on `releases\v1.1.0-rc.5\GuaranteeManager.exe`. |
| Real report duration unknown | Very large Excel exports may still be slow even though they no longer block repeated launches. | Monitor report duration during pilot and cap/report known slow exports if needed. |

### Pilot Approval

- Scope: Today, Guarantees, Banks, Settings backup/restore, and Reports with monitored usage.
- Users: `3-5` operational users plus one admin/support owner.
- Data size limit: start around `500` current guarantees; `5,000` only in monitored read/report testing.
- Duration: `10 business days`.
- Backup requirement: manual backup every business day before work ends; portable package at least twice per week.
- Support channel: one named channel with issue timestamp, user, workspace, guarantee number, and logs attached.
- Monitor: `Logs`, `ui-events.jsonl`, backup failures, `_staging` folders, report duration, app hangs, missing attachments, and workflow supersession behavior.
- Rollback condition: suspected data loss, failed restore, missing attachment evidence, incorrect terminal workflow state, repeated backup failure, or report freeze during core work.

## Historical Release Gate Review Addendum (v1.1.0-rc.4, Superseded)

**Review date:** 2026-05-03
**Reviewed version:** `v1.1.0-rc.4`
**Branch:** `feature/v1.1-operational-polish`
**Final release decision:** `Approved with blockers`
**Score:** `7.0/10`

This addendum captures the release-gate decision made after running the required build, test, and publish checks. It does not replace the remaining implementation list below; it classifies which remaining items affect pilot, production, and broad rollout readiness.

### Command Evidence

| Check | Result | Evidence |
|---|---|---|
| `git status --short --branch` | Not clean | Branch is `feature/v1.1-operational-polish`; working tree had `223` status entries. |
| `git log --oneline -10` | Collected | Latest commit shown: `181ffed Use app chrome for dialog windows`. |
| `dotnet build GuaranteeManager.csproj --no-restore` | Pass | `0 Warning(s) / 0 Error(s)`. |
| `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore` | Pass | `158/158` passed, `0` skipped. |
| `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release --no-restore` | Pass | `158/158` passed, `0` skipped. |
| `.\scripts\publish_release.ps1` | Pass | Published `v1.1.0-rc.4` and created local zip artifact. |

Release artifact:

- Publish directory: `releases\v1.1.0-rc.4`
- Archive: `releases\GuaranteeManager_v1.1.0-rc.4_win-x64.zip`
- Artifact contains `GuaranteeManager.exe` and copied release `README.md`.

Important release-management discrepancy:

- The generated artifact `README.md` still reports `143/143` tests, while the actual verified count is `158/158`.
- The local release artifact exists, but the source baseline is not cleanly reproducible until repo hygiene is fixed.

### Gate Review

| Gate | Status | Evidence | Blocking Issues |
|---|---|---|---|
| Build & Tests | Pass | Build, Debug tests, Release tests, and publish script all passed. No skipped tests and no test conditional compilation were found. | None for pilot. |
| Data Safety | Pass with concerns | Hard-delete triggers block guarantee and attachment deletion; versioning keeps history; workflow execution uses transactions; backup/restore tests cover normal and corrupt-source cases. | Durable file promotion and cleanup queue is still missing before expansion. Extreme backup/database-lock coverage remains incomplete. |
| Business Rules | Pass with concerns | Tests cover extension, reduction, release, liquidation, replacement, pending supersession, expired guarantee handling, admin edit guard, and timeline behavior. | Remaining edge tests are still useful before wider rollout. |
| Performance | Fail | Main Guarantees workspace uses SQL pagination; Dashboard and Banks loading were reduced/capped. | Reports still run synchronously on the UI thread; no large-dataset guard exists; `5,000` guarantee behavior is not proven. |
| UX | Pass with concerns | Arabic/RTL shell and dialogs are present; validation and terminal-action confirmations exist; backup/restore failures are visible from Settings. | No fresh published-artifact UI acceptance run was executed in this gate; automatic backup failure is logged but not prominent to the user. |
| Operational Readiness | Fail | Artifact exists, logs exist, quick/user guides exist. | Working tree is not clean; moved source directories are untracked/ignored; no clean `v1.1.0-rc.4` tag; artifact README test count is stale. |

### Release Blockers

These blockers must be resolved before using this build as a release baseline beyond a controlled internal pilot.

| Blocker | Evidence | Required Fix | Owner/Area |
|---|---|---|---|
| Release baseline is not reproducible from source control | Working tree had `223` status entries. Source files moved under new folders are untracked, and `Services\Attachments` plus `Services\Workflow` are ignored by `.gitignore`. No `v1.1.0-rc.4` tag exists. | Fix ignore rules, stage/commit all source moves, rebuild from a clean checkout, rerun required checks, republish, and tag the baseline. | Release / Repository |
| Large reports can freeze the app during real-data use | Report execution remains synchronous through `ReportsWorkspaceCoordinator`, `WorkspaceReportCatalog`, and `IExcelService`. | Make report execution async with progress and disabled run button, or explicitly limit/disable large exports for pilot. | Reports / Performance |

### High Priority Before Expansion

| Issue | Risk | Fix |
|---|---|---|
| Durable file promotion queue missing | Attachment DB rows can exist while file promotion needs manual recovery. | Add persistent retry/cleanup queue and diagnostics. |
| Backup edge cases not covered | Disk-full, `SQLITE_BUSY`, and corrupt portable-envelope cases may behave unpredictably. | Add resiliency tests and user-facing failure handling. |
| No large-dataset regression | Future changes may reintroduce full-table UI paths. | Add `500` and `5,000` record smoke/regression tests. |
| Release docs stale | Artifact README says `143/143`, actual gate result is `158/158`. | Update release notes before republish. |
| UX acceptance not rerun on artifact | Visual/RTL regressions may be missed. | Run focused UAT/smoke on the published package. |

### Pilot Plan After Blockers Close

- Scope: Today, Guarantees, Banks, Settings backup/restore, and limited Reports.
- Users: `3-5` operational users plus one admin/support owner.
- Data size limit: up to about `500` current guarantees; avoid `5,000` scale until measured.
- Duration: `10 business days`.
- Backup requirement: manual backup every business day before work ends; portable package at least twice per week.
- Support channel: one named channel with issue timestamp, user, workspace, guarantee number, and logs attached.
- Monitor: `Logs`, `ui-events.jsonl`, backup failures, `_staging` folders, report duration, app hangs, missing attachments, and workflow supersession behavior.
- Rollback condition: suspected data loss, failed restore, missing attachment evidence, incorrect terminal workflow state, repeated backup failure, or report freeze during core work.

### Rollback Plan

- Preserve current database before pilot by creating a manual backup and portable package from Settings.
- Keep a copy of `%LOCALAPPDATA%\GuaranteeManager` before rollout.
- Restore through Settings for `.db` backups, or portable package restore when database plus attachments/workflow files must be restored together.
- Collect logs from `%LOCALAPPDATA%\GuaranteeManager\Logs\log_*.txt`, `audit_*.txt`, `ui-events.jsonl`, and `ui-shell-state.json`.
- Stop rollout by removing access to the new zip/shortcut and instructing users to close the app.
- Communicate the issue, rollback time, impacted records, and when data entry can resume.

### Known Limitations To Document Before Pilot

- This build is not ready for limited or broad production as-is.
- Large Excel reports may block the UI.
- `5,000` guarantee behavior is not verified.
- Attachment recovery exists, but durable retry tracking is not implemented.
- Extreme backup/restore failure modes still need tests.
- Automatic backup failure is logged but not prominent to the user.
- Current artifact documentation has stale test counts.

### Plain Release Decision

1. Can this version be used with real data? Not as-is. After the two release blockers are closed and the artifact is rebuilt from a clean baseline, yes for a controlled internal pilot only.
2. By how many users? `3-5` users plus one admin/support owner.
3. Under what constraints? Daily backups, data capped around `500` guarantees, limited report usage, monitored logs, and immediate rollback on data-safety issues.
4. What must be fixed before wider rollout? Async/report resilience, durable file promotion queue, backup edge tests, large-dataset coverage, clean tagged release baseline, and corrected release docs.
5. What is the release decision? `Approved with blockers`.

## Clear Implementation Plan

This plan is ordered by release risk. Do not start pilot use with real data until Phase 0 and Phase 1 are complete and verified from a clean release baseline.

### Phase 0 - Make The Release Baseline Reproducible

**Goal:** turn the current local working tree into a clean, rebuildable `v1.1.0-rc.4` or next RC baseline.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 0.1 | Fix `.gitignore` so runtime folders remain ignored without accidentally ignoring source folders such as `Services\Attachments` and `Services\Workflow`. | `.gitignore` | `git check-ignore -v Services\Attachments\AttachmentStorageService.cs Services\Workflow\WorkflowService.cs` should return no ignore rule. |
| 0.2 | Stage the moved service files and deleted legacy flat `Services/*.cs` paths as intentional renames/restructure. | `Services/**` | `git status --short Services` should show tracked changes, not ignored source files. |
| 0.3 | Update stale release documentation from `143/143` to `158/158`. | `Doc/releases/README_v1.1.0-rc.4.md`, generated package README after publish | Release notes match actual command evidence. |
| 0.4 | Decide whether the version remains `v1.1.0-rc.4` or becomes `v1.1.0-rc.5` because packaging/repo hygiene changed. | `GuaranteeManager.csproj`, release README | Version choice is consistent across project file, docs, artifact name, and app display. |
| 0.5 | Commit the release baseline after successful verification. | Repository | Working tree is clean except intentionally ignored build/runtime output. |
| 0.6 | Rebuild and republish from the clean baseline. | `scripts/publish_release.ps1` | Required release commands pass again. |
| 0.7 | Tag the verified release candidate. | Git tag | `git tag --list "v1.1.0-rc.*"` includes the verified RC tag. |

Exit criteria:

- `git status --short --branch` is clean before publishing.
- No production source file is ignored by `.gitignore`.
- Release artifact and release README both report the same version and test count.
- Required commands pass:
  - `dotnet build GuaranteeManager.csproj --no-restore`
  - `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore`
  - `dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release --no-restore`
  - `.\scripts\publish_release.ps1`

### Phase 1 - Remove The Pilot-Blocking Report Freeze Risk

**Goal:** make report/export execution safe enough for pilot users.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 1.1 | Convert report execution from synchronous `TryRun` into an async command path. | `Presentation/Views/Reports/ReportsWorkspaceCoordinator.cs` | Running a report does not block the UI thread. |
| 1.2 | Add running state to the Reports surface: disable run button, keep selected report visible, show status/progress text. | `Presentation/Views/Reports/ReportsWorkspaceSurface.cs` | User cannot start duplicate report runs accidentally. |
| 1.3 | Keep `LastOutputPath`, Arabic error messages, and open-last-output behavior intact. | `ReportsWorkspaceCoordinator`, `ExcelService` | Existing report tests still pass. |
| 1.4 | Add focused tests for success, failure, and "already running" behavior where practical. | `GuaranteeManager.Tests` | Tests cover result state and failure message preservation. |
| 1.5 | If full async implementation is too large for the RC, add explicit pilot guardrails: warn before broad exports and document data/report limits. | Reports UI, release docs | Users see the limitation before running large exports. |

Exit criteria:

- Reports workspace stays responsive during export generation.
- User sees clear Arabic progress/result/error state.
- Release notes document any remaining report-size limitation.

### Phase 2 - Strengthen Attachment And File/Data Coordination

**Goal:** reduce risk where database commits succeed but file promotion or cleanup needs later recovery.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 2.1 | Add a durable pending file-operation store for attachment promotion and cleanup. | `Services/Database/*`, `Services/Attachments/AttachmentStorageService.cs` | Pending operations survive app restart. |
| 2.2 | Retry pending promotions and orphan cleanup during startup/background maintenance. | `DatabaseRuntimeInitializer`, attachment service | Restart recovers staged files without manual intervention. |
| 2.3 | Record unrecoverable file-operation failures in logs and UI diagnostics. | `SimpleLogger`, `UiDiagnosticsService` | Failure is visible in logs/diagnostics. |
| 2.4 | Replace silent cleanup-only behavior with tracked best-effort cleanup. | Workflow and attachment paths | Failed cleanup is not lost silently. |
| 2.5 | Add tests for deferred promotion recovery, failed cleanup persistence, and startup retry. | `DatabaseGuaranteePersistenceTests`, new focused tests | Failure/retry paths are covered. |

Exit criteria:

- A DB/file mismatch has a durable recovery path.
- `_staging` files are either recovered, cleaned, or reported.
- No historical attachment metadata is hard-deleted.

### Phase 3 - Add Backup And Database-Lock Resilience Tests

**Goal:** prove backup/restore behavior under the failure modes most likely to hurt real data.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 3.1 | Test manual backup write failure or blocked target path. | `DatabaseBackupWorkflowTests`, `BackupService` | Failed manual backup does not corrupt current DB. |
| 3.2 | Test automatic backup failure does not rotate out valid backups. | `BackupService`, backup tests | Valid older backups remain available. |
| 3.3 | Test `SQLITE_BUSY` or locked database behavior for backup/restore and critical writes. | Backup/repository tests | Error is safe and actionable. |
| 3.4 | Test missing/corrupt portable package manifest/key envelope. | `PortableBackupPackageCryptoTests`, `PortableBackupPackageUtility` | Current DB and files are not replaced. |
| 3.5 | Surface automatic backup failure more visibly to the user if feasible. | `App.xaml.cs`, shell status/diagnostics | Startup backup warning is visible, not only logged. |

Exit criteria:

- Backup failure paths preserve current database and existing valid backups.
- Restore failure paths do not partially replace database, attachments, or workflow files.
- User/admin can find the failure in UI status or logs.

### Phase 4 - Add Large-Dataset And Pagination Guards

**Goal:** make performance claims measurable before expanding beyond pilot.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 4.1 | Add a `500` guarantee test dataset smoke that exercises the main Guarantees workspace query path. | `GuaranteeWorkspaceDataServiceTests`, repository tests | Page size and SQL `LIMIT/OFFSET` remain enforced. |
| 4.2 | Add a `5,000` guarantee non-UI repository/workspace performance guard. | `GuaranteeQueryTests`, test helpers | Query count/time is acceptable or documented. |
| 4.3 | Assert Dashboard and Banks do not reintroduce full unbounded loading. | `ShellWorkspaceFactory`, dashboard/banks tests | Dashboard caps and SQL aggregation remain intact. |
| 4.4 | Add search debounce or delayed execution for SQLite-backed search boxes if real use shows repeated query pressure. | Shell/workspace surfaces | Typing does not trigger excessive DB work. |
| 4.5 | Add report record-count preview or warning before large exports. | Reports workspace/catalog | User sees size before export. |

Exit criteria:

- `500` guarantees are usable.
- `5,000` guarantees are either acceptable by test evidence or documented as outside pilot scope.
- Reports/export limits are visible to users.

### Phase 5 - Pilot Packaging, UAT, And Rollback Readiness

**Goal:** create a pilot package that support can operate safely.

| Step | Work | Primary files/areas | Verification |
|---|---|---|---|
| 5.1 | Run focused published-artifact UAT on the actual zip contents. | `releases/<version>` | App opens, version shows, core workspaces load. |
| 5.2 | Smoke test core user flows: new guarantee, extension, reduction, release, replacement, attachment, report, backup, restore. | Published app | Results appear in timeline and logs. |
| 5.3 | Confirm Arabic/RTL dialogs, terminal confirmations, and backup/restore messages. | UI | Non-technical user can follow messages. |
| 5.4 | Prepare pilot runbook with data size, user count, backup cadence, support channel, and rollback trigger. | Release docs / guide | Pilot owner can operate without developer context. |
| 5.5 | Create final pilot artifact and preserve hash/path. | `releases/` | Artifact path and version are recorded. |

Exit criteria:

- Pilot package is built from a clean tagged baseline.
- UAT smoke has no blockers.
- Rollback procedure is tested or rehearsed.

### Final Release Gate After Plan Completion

Run the release gate again after Phase 0 and Phase 1 at minimum, and again before limited production after Phases 2-5:

```powershell
git status --short --branch
git log --oneline -10
dotnet build GuaranteeManager.csproj --no-restore
dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj --no-restore
dotnet test GuaranteeManager.Tests\GuaranteeManager.Tests.csproj -c Release --no-restore
.\scripts\publish_release.ps1
```

Decision rules:

- If Phase 0 fails: `Not approved`.
- If Phase 1 fails: `Approved with blockers` only; no pilot with large reports.
- If Phases 0-1 pass but Phases 2-5 remain open: `Approved for internal pilot`.
- If Phases 0-5 pass with clean UAT: consider `Approved for limited production`.
- Do not consider broad internal rollout until durable file operations, large-dataset evidence, backup resilience, and operational docs are all complete.

## P0 - Before Broad Release

### 1. Run Excel Report Generation Off The UI Thread

Current state: backup and restore work was moved to background tasks, but report generation still runs synchronously through `ReportsWorkspaceCoordinator.TryRun`, `WorkspaceReportCatalog.Run`, and `IExcelService`.

Required work:
- Make report execution asynchronous from the Reports workspace.
- Disable the run button while a report is running and show progress/status.
- Keep the UI responsive during large ClosedXML workbook generation.
- Preserve Arabic error handling and `LastOutputPath` behavior.

Primary areas:
- `Presentation/Views/Reports/ReportsWorkspaceCoordinator.cs`
- `Presentation/Views/Reports/ReportsWorkspaceSurface.cs`
- `Utils/WorkspaceReportCatalog.cs`
- `Services/Reports/ExcelService.cs`

### 2. Add A Durable File Promotion And Cleanup Queue

Current state: staged attachment recovery exists and is tested, but file/database coordination still depends on retry/recovery patterns instead of a durable operation queue.

Required work:
- Track deferred file promotions and failed cleanup attempts persistently.
- Retry pending file promotions and orphan cleanup on startup and/or background maintenance.
- Surface unrecoverable file-operation failures in diagnostics.
- Reduce reliance on silent `TryDeleteFile` cleanup paths.

Primary areas:
- `Services/Attachments/AttachmentStorageService.cs`
- `Services/Workflow/WorkflowRequestCreator.cs`
- `Services/Workflow/WorkflowResponseRecorder.cs`
- `Services/Database/DatabaseRuntimeInitializer.cs`

### 3. Add Resiliency Tests For Backup And Database-Locked Scenarios

Current state: normal backup/restore and several failure paths are covered, but the audit reports still call out extreme IO and locking cases.

Required work:
- Test disk-full or write-failure behavior for auto backup and manual backup.
- Test `SQLITE_BUSY` or locked database behavior during backup/restore and critical writes.
- Test missing/corrupt portable-backup key/envelope handling explicitly.
- Ensure failed backup artifacts do not rotate out valid backups.

Primary areas:
- `GuaranteeManager.Tests/DatabaseBackupWorkflowTests.cs`
- `GuaranteeManager.Tests/PortableBackupPackageCryptoTests.cs`
- `Services/Backup/BackupService.cs`
- `Services/Backup/SqliteBackupUtility.cs`

### 4. Add Large Dataset Regression Coverage

Current state: unbounded loading in Banks and Dashboard was reduced, and pending-root loading was improved. The suite still lacks a meaningful large-dataset guard.

Required work:
- Add a test dataset large enough to catch accidental full-table UI paths.
- Assert paging/limits on guarantee queries used by dashboard and workspaces.
- Add a lightweight performance smoke test for report catalog queries where practical.
- Add search debouncing or delayed execution for search boxes that query SQLite on every text change.

Primary areas:
- `Presentation/Shell/ShellWorkspaceFactory.cs`
- `Presentation/Views/Guarantees/GuaranteeWorkspaceDataService.cs`
- `Presentation/Views/Dashboard/DashboardWorkspaceSurface.cs`
- `GuaranteeManager.Tests`

## P1 - Architecture Refactoring

### 5. Move Workspace Data Services Out Of Presentation

Current state: `GuaranteeWorkspaceDataService` and related workspace data builders still mix application queries, formatting, and presentation DTO construction.

Required work:
- Move reusable query/application logic to `Services` or an application layer.
- Return raw application DTOs from services.
- Keep WPF-specific formatting and brushes in Presentation.
- Preserve current tests while separating service tests from UI model tests.

Primary areas:
- `Presentation/Views/Guarantees/GuaranteeWorkspaceDataService.cs`
- `Presentation/Views/Dashboard/DashboardWorkspaceDataService.cs`
- `Presentation/Views/Banks/BanksWorkspaceDataService.cs`

### 6. Centralize Domain Validation And Normalization

Current state: amount validation, amount normalization, beneficiary normalization, and related business rules still appear in repositories and workflow services.

Required work:
- Introduce a domain/application validation service for guarantee amounts, dates, beneficiary defaults, and party normalization.
- Call it before repository persistence.
- Keep repository methods focused on persistence and integrity checks.

Primary areas:
- `Services/Repositories/GuaranteeRepository.cs`
- `Services/Workflow/WorkflowRequestCreator.cs`
- `Services/Workflow/WorkflowReplacementExecutor.cs`
- `Services/Workflow/WorkflowNewVersionExecutor.cs`

### 7. Replace Workflow Response Switches With Handlers

Current state: workflow execution still uses type switches in core execution paths. It is functional but less extensible for future workflow types.

Required work:
- Introduce per-request-type handlers or a strategy registry.
- Keep transaction boundaries explicit.
- Preserve existing workflow invariant tests.

Primary areas:
- `Services/Workflow/WorkflowResponseRecorder.cs`
- `Services/Workflow/WorkflowExecutionProcessor.cs`
- `Services/Database/WorkflowExecutionDataAccess.cs`

### 8. Improve Persistence Error Specificity

Current state: many persistence errors are wrapped generically. This is safe but not always actionable.

Required work:
- Distinguish common `SqliteException` cases such as constraint violations, locked database, missing path, and write failure.
- Preserve user-safe Arabic messages.
- Add diagnostic details without exposing raw internal errors to normal users.

Primary areas:
- `Services/Repositories/GuaranteeRepository.cs`
- `Services/Repositories/WorkflowRequestRepository.cs`
- `Services/Core/OperationFailure.cs`

## P2 - Workflow Auditability

### 9. Make Timeline Links More Explicit For Replacement And Admin Edit

Current state: data links exist through `ReplacesRootId` and `ReplacedByRootId`, and timeline backfill works, but the reports still call out audit readability gaps.

Required work:
- Add visible timeline entries or navigation affordances linking original and replacement guarantees.
- Distinguish administrative edit versions from workflow-created versions in timeline details.
- Consider eager event creation for key events instead of relying only on lazy backfill.

Primary areas:
- `Services/Guarantees/GuaranteeEventStore.cs`
- `Presentation/Shell/Models/GuaranteeTimelineModels.cs`
- `Presentation/Views/Guarantees/GuaranteeWorkspaceDataService.cs`

### 10. Add Remaining Workflow Edge Tests

Current state: major workflow issues are fixed. A few edge tests from the reports remain useful.

Required work:
- Concurrent replacement requests using the same replacement guarantee number: assert only one can succeed.
- Extension request after legitimate supplier-only admin edit: assert extension still executes correctly.
- Replacement timeline navigation: assert old and new roots are discoverable from each other in the UI model.

Primary areas:
- `GuaranteeManager.Tests/WorkflowTerminalLifecycleTests.cs`
- `GuaranteeManager.Tests/WorkflowResponseVersioningTests.cs`
- `GuaranteeManager.Tests/GuaranteeWorkspaceDataServiceTests.cs`

## P3 - UX And Reporting Enhancements

### 11. Add Report Preview / Export Current View

Current state: report exports work, and file-open errors are now more actionable. The reports still identify missing preview/current-view features.

Required work:
- Add a preview/detail state in Reports before export.
- Add "export current view" from the Guarantees workspace using the active filters/search/page scope as designed.
- Show estimated record count before large exports.

Primary areas:
- `Presentation/Views/Reports`
- `Presentation/Views/Guarantees`
- `Utils/WorkspaceReportCatalog.cs`

### 12. Add Search Highlights

Current state: search filters results, but matching text is not highlighted.

Required work:
- Highlight matched text in guarantee, bank, report, and dashboard lists where practical.
- Keep Arabic/RTL layout stable.

Primary areas:
- `Presentation/Views/Guarantees`
- `Presentation/Views/Banks`
- `Presentation/Views/Dashboard`
- `Presentation/Views/Reports`

### 13. Add Attachment Thumbnails / Type Previews

Current state: attachments have document types and open actions, but no thumbnail/preview affordance.

Required work:
- Show a small PDF/image/type preview in attachment lists.
- Avoid loading large files synchronously in the UI.
- Use safe fallbacks for missing or unsupported files.

Primary areas:
- `Presentation/Views/Guarantees`
- `Presentation/Dialogs/AttachmentPickerDialog.cs`
- `Presentation/Shell/Models/GuaranteePreviewModels.cs`

### 14. Improve Dialog And Input Micro-Interactions

Current state: core validation is safe. The remaining items are UX polish.

Required work:
- Add a "checking" state for guarantee-number uniqueness if validation becomes slow.
- Audit RTL dialog button order for consistency.
- Add bank-response note suggestions based on selected response status.
- Review punctuation rendering in mixed LTR/RTL containers.

Primary areas:
- `Presentation/Dialogs/NewGuaranteeDialog.cs`
- `Presentation/Dialogs/ReplacementRequestDialog.cs`
- `Presentation/Dialogs/BankResponseDialog.cs`
- `Presentation/Dialogs/DialogChrome.cs`

## No Remaining Implementation From Security Report

The security/reliability report did not leave confirmed implementation findings after filtering. It is retained here only as a source that was reviewed and consolidated.
