# System layer coverage

Reconciled 2026-10-19 against `src/Compliance.Common` and `src/Compliance.Core`. The inventories are per-type: each C# type belongs in its own matching file under the owning feature directory. Presence means the source file exists; it does not mean a workflow is behaviorally complete, registered, authorized, exposed, or released.

## Current source

| Layer | Coded types | Inventory |
| --- | ---: | --- |
| Request/command contracts | 235 source contracts | [Existing commands](component-inventory.md#existing-commands) and [command list](command-list.md) |
| Query contracts | 272 (216 existing + 56 backfilled contracts) | [Existing queries](component-inventory.md#existing-queries) and [required query/view-model backfill](query-and-view-model-list.md) |
| Domain events | 392 declared source types; 381 in the tracked feature-backed universe, with 11 declarations to reconcile | [Existing events](component-inventory.md#existing-events) and [required event backfill](domain-event-list.md) |
| Aggregates | 85 (68 existing + 17 TDD-backed aggregates) | [Existing aggregates](component-inventory.md#existing-aggregates) |
| Models, views, and supporting contracts | 569 (436 existing + 59 response/receipt views + 74 aggregate payload/support models) | [Existing models](component-inventory.md#existing-models-and-supporting-contracts) and [required query/view-model backfill](query-and-view-model-list.md) |

All source links in those existing inventories resolve to files. The command list separates existing contracts from additional candidates and internal operations. The component inventory contains the proposed future queries, events, aggregate/model candidates, reactors, and projectors.

## Remaining source types

The former broad proposal list mixed required work with hypotheses and unresolved policy scope. It is superseded as an implementation plan by the pruned, story-linked [system command list](command-list.md). Five evidence command contracts are present per file: `CaptureEvidenceArtifact`, `RecordEvidenceArtifactInspection`, `IssueArtifactDownload`, `LinkEvidenceSupport`, and `ReleaseEvidenceArtifactQuarantine`, each under `src/Compliance.Common/Features/Evidence/`. All five now have Core handlers; download, support-link and quarantine-release decisions also persist aggregate facts. Upload API exposure, scanner-provider qualification, and other transport or worker integration remain explicitly called out in the command and handler checklists. The 130-command plan contains 5 commands explicitly deferred by M0-D04/EN-05; the [handler coverage checklist](handler-coverage-checklist.md) tracks active implementation separately from those deferred imports.

The pruned plan retains 130 feature-backed command requirements, each mapped to a typed command contract; actual-staff revocation reuses its already wired contract and handler. Five readiness-material import commands are explicitly deferred, leaving 125 active handler requirements. Excluded P2 hypotheses, unsettled BO scope, and unvalidated Privacy scope are not counted as required product commands.

The domain-event backfill identifies 141 typed contracts; the four weekly-digest and five Type II calendar lifecycle events extend the tracked feature-backed universe to 381. Eight readiness-import events remain deferred, and 11 declarations in the current source scan still need reconciliation to accepted feature requirements. The R2-08 `AdvisorHandoff`, T1-01 `ExaminationEngagement`, T1-02 `SystemDescription`, T1-04 `AuditorRequest`, R2-12 evidence-governance roots, T1-05/T3-05 `AuditPackage`, T1-07 examination transition roots, and T2-01/T3-01 `TypeIIPeriod` plus `TypeIIObservationCalendar` have focused RED-to-GREEN behavior tests. All 21 Type I baseline source families are registered and covered by source-contributor tests. Handler coverage is 114 of 125 active commands complete, 3 partial, and 8 without handlers. The Type II proposal and period start now preserve exact approved-control expected-evidence descriptions through their plan and occurrence records. Five readiness-import commands remain explicitly deferred. The [handler checklist](handler-coverage-checklist.md) tracks active gaps and prerequisites. Reactors follow handler closure; projectors are the final layer.

## Validation

The seven existing source inventories (commands, queries, events, aggregates, models, reactors, and projectors) were checked by resolving every linked `.cs` path. The Type I baseline preview composes all 21 required source families, and engagement creation requires an approved readiness entry decision. The focused examination command-handler suite passes 16 tests; all 4,677 non-broker/non-web backend tests pass; the Release build succeeds with zero warnings or errors; and whitespace formatting verification passes. The advisor handoff list authorizes assigned Advisors and scoped guests separately, limits their results to unexpired ready handoffs, and rechecks visibility after reading. Evidence-support links bind an exact artifact and target revision and cannot claim a period outside the artifact's captured coverage; valid reuse stays inside that recorded interval. The close-freeze handler remains partial until the source-backed close-validation command is implemented. Candidate counts are name-level coverage only and do not imply runtime wiring.
