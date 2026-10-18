# System layer coverage

Reconciled 2026-10-17 against `src/Compliance.Common` and `src/Compliance.Core`. The inventories are per-type: each C# type belongs in its own matching file under the owning feature directory. Presence means the source file exists; it does not mean a workflow is behaviorally complete, registered, authorized, exposed, or released.

## Current source

| Layer | Coded types | Inventory |
| --- | ---: | --- |
| Request/command contracts | 230 inventoried contracts, plus 5 new evidence contracts | [Existing commands](component-inventory.md#existing-commands) and [command list](command-list.md) |
| Query contracts | 271 (216 existing + 55 backfilled contracts) | [Existing queries](component-inventory.md#existing-queries) and [required query/view-model backfill](query-and-view-model-list.md) |
| Domain events | 372 (236 existing + 136 backfilled contracts) | [Existing events](component-inventory.md#existing-events) and [required event backfill](domain-event-list.md) |
| Aggregates | 84 (68 existing + 16 TDD-backed aggregates) | [Existing aggregates](component-inventory.md#existing-aggregates) |
| Models, views, and supporting contracts | 561 (436 existing + 54 response views + 71 aggregate payload/support models) | [Existing models](component-inventory.md#existing-models-and-supporting-contracts) and [required query/view-model backfill](query-and-view-model-list.md) |

All source links in those existing inventories resolve to files. The command list separates existing contracts from additional candidates and internal operations. The component inventory contains the proposed future queries, events, aggregate/model candidates, reactors, and projectors.

## Remaining source types

The former broad proposal list mixed required work with hypotheses and unresolved policy scope. It is superseded as an implementation plan by the pruned, story-linked [system command list](command-list.md). Five evidence command contracts are present per file: `CaptureEvidenceArtifact`, `RecordEvidenceArtifactInspection`, `IssueArtifactDownload`, `LinkEvidenceSupport`, and `ReleaseEvidenceArtifactQuarantine`, each under `src/Compliance.Common/Features/Evidence/`. All five now have Core handlers; download, support-link and quarantine-release decisions also persist aggregate facts. Upload API exposure, scanner-provider qualification, and other transport or worker integration remain explicitly called out in the command and handler checklists. Across the 127 required additions, the [handler coverage checklist](handler-coverage-checklist.md) still tracks the remaining command handlers.

The pruned plan retains 127 feature-backed command requirements, and each has a typed command contract file. Excluded P2 hypotheses, unsettled BO scope, and unvalidated Privacy scope are not counted as required product commands.

The domain-event backfill adds 136 typed per-file contracts from accepted backlog requirements; 8 readiness-import events remain deferred. The R2-08 `AdvisorHandoff`, T1-01 `ExaminationEngagement`, T1-02 `SystemDescription`, T1-04 `AuditorRequest`, R2-12 evidence-governance roots, T1-05/T3-05 `AuditPackage`, T1-07 examination transition roots, and T2-01/T3-01 `TypeIIPeriod` have focused RED-to-GREEN behavior tests. Required handler work is in progress: 60 of 127 commands are implemented and registered, with static test references for 61. The [handler checklist](handler-coverage-checklist.md) tracks the remaining 67. Projectors and reactors follow this layer.

## Validation

The seven existing source inventories (commands, queries, events, aggregates, models, reactors, and projectors) were checked by resolving every linked `.cs` path. The new evidence command contracts compiled in the Release solution build with zero warnings and errors; formatting verification passed. Candidate counts are name-level coverage only and do not imply runtime wiring.
