# System layer coverage

Reconciled 2026-10-18 against `src/Compliance.Common` and `src/Compliance.Core`. The inventories are per-type: each C# type belongs in its own matching file under the owning feature directory. Presence means the source file exists; it does not mean a workflow is behaviorally complete, registered, authorized, exposed, or released.

## Current source

| Layer | Coded types | Inventory |
| --- | ---: | --- |
| Request/command contracts | 236 contracts (230 inventoried, 5 evidence, 1 audit-package creation) | [Existing commands](component-inventory.md#existing-commands) and [command list](command-list.md) |
| Query contracts | 271 (216 existing + 55 backfilled contracts) | [Existing queries](component-inventory.md#existing-queries) and [required query/view-model backfill](query-and-view-model-list.md) |
| Domain events | 392 declared source types; 381 in the tracked feature-backed universe, with 11 declarations to reconcile | [Existing events](component-inventory.md#existing-events) and [required event backfill](domain-event-list.md) |
| Aggregates | 85 (68 existing + 17 TDD-backed aggregates) | [Existing aggregates](component-inventory.md#existing-aggregates) |
| Models, views, and supporting contracts | 565 (436 existing + 55 response/receipt views + 74 aggregate payload/support models) | [Existing models](component-inventory.md#existing-models-and-supporting-contracts) and [required query/view-model backfill](query-and-view-model-list.md) |

All source links in those existing inventories resolve to files. The command list separates existing contracts from additional candidates and internal operations. The component inventory contains the proposed future queries, events, aggregate/model candidates, reactors, and projectors.

## Remaining source types

The former broad proposal list mixed required work with hypotheses and unresolved policy scope. It is superseded as an implementation plan by the pruned, story-linked [system command list](command-list.md). Five evidence command contracts are present per file: `CaptureEvidenceArtifact`, `RecordEvidenceArtifactInspection`, `IssueArtifactDownload`, `LinkEvidenceSupport`, and `ReleaseEvidenceArtifactQuarantine`, each under `src/Compliance.Common/Features/Evidence/`. All five now have Core handlers; download, support-link and quarantine-release decisions also persist aggregate facts. Upload API exposure, scanner-provider qualification, and other transport or worker integration remain explicitly called out in the command and handler checklists. The 128-command plan contains 5 commands explicitly deferred by M0-D04/EN-05; the [handler coverage checklist](handler-coverage-checklist.md) tracks active implementation separately from those deferred imports.

The pruned plan retains 129 feature-backed command requirements, each with a typed command contract. Five readiness-material import commands are explicitly deferred, leaving 124 active handler requirements. Excluded P2 hypotheses, unsettled BO scope, and unvalidated Privacy scope are not counted as required product commands.

The domain-event backfill identifies 141 typed contracts; the four weekly-digest and five Type II calendar lifecycle events extend the tracked feature-backed universe to 381. Eight readiness-import events remain deferred, and 11 declarations in the current source scan still need reconciliation to accepted feature requirements. The R2-08 `AdvisorHandoff`, T1-01 `ExaminationEngagement`, T1-02 `SystemDescription`, T1-04 `AuditorRequest`, R2-12 evidence-governance roots, T1-05/T3-05 `AuditPackage`, T1-07 examination transition roots, and T2-01/T3-01 `TypeIIPeriod` plus `TypeIIObservationCalendar` have focused RED-to-GREEN behavior tests. Handler coverage is 94 of 124 active commands complete, 7 partial, and 23 without handlers. Five readiness-import commands remain explicitly deferred. The [handler checklist](handler-coverage-checklist.md) tracks active gaps and prerequisites. Projectors and reactors follow this layer.

## Validation

The seven existing source inventories (commands, queries, events, aggregates, models, reactors, and projectors) were checked by resolving every linked `.cs` path. The Type II calendar aggregate, close-freeze handler, event contracts, and OpenAPI routes compile; all 4,171 non-broker/non-web tests, the focused OpenAPI test, and formatting verification pass. The close-freeze handler remains partial until the source-backed close-validation command is implemented. Candidate counts are name-level coverage only and do not imply runtime wiring.
