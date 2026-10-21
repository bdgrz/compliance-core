# System layer coverage

Product-scope counts below were reconciled 2026-10-19 against
`src/Compliance.Common` and `src/Compliance.Core`. Aggregate and command-handler
source coverage was refreshed 2026-10-21 in the linked audit. The inventories
are per-type: each C# type belongs in its own matching file under the owning
feature directory. Presence means the source file exists; it does not mean a
workflow is behaviorally complete, registered, authorized, exposed, or
released.

## Current source

| Layer | Coded types | Inventory |
| --- | ---: | --- |
| Request/command contracts | 238 source contracts | [Existing commands](component-inventory.md#existing-commands) and [command list](command-list.md) |
| Query contracts | 272 (216 existing + 56 backfilled contracts) | [Existing queries](component-inventory.md#existing-queries) and [required query/view-model backfill](query-and-view-model-list.md) |
| Domain events | 404 declared source types; 403 accepted facts and 1 explicitly excluded migration event | [Existing events](component-inventory.md#existing-events) and [source declaration reconciliation](domain-event-list.md#source-declaration-reconciliation) |
| Aggregates | 99 source aggregate types | [Existing aggregates](component-inventory.md#existing-aggregates) |
| Models, views, and supporting contracts | 569 (436 existing + 59 response/receipt views + 74 aggregate payload/support models) | [Existing models](component-inventory.md#existing-models-and-supporting-contracts) and [required query/view-model backfill](query-and-view-model-list.md) |
| Core handler coverage | 114 complete, 3 partial, and 8 missing of 125 active commands | [Handler coverage checklist](handler-coverage-checklist.md) |
| Reactors | 33 source types | [Existing reactors](component-inventory.md#existing-reactors); critical-flow coverage remains open |
| Projectors | 78 source types | [Existing projectors](component-inventory.md#existing-projectors); query-to-projector coverage remains open |
| HTTP routes | 493 typed registrations and 1 custom route | [HTTP API inventory](component-inventory.md#existing-http-apis); 44 query routes remain unregistered |

All source links in those existing inventories resolve to files. The command list separates existing contracts from additional candidates and internal operations. The component inventory contains the proposed future queries, events, aggregate/model candidates, reactors, and projectors.

The [aggregate and command-handler audit](aggregate-command-coverage-audit.md)
maps each current event contract to its reducer and emitter, and each request
to its handler, registration, and aggregate route. At its captured source
commit, 381 aggregate-emitted facts have reducers, all 99 aggregate roots have
a command or internal reaction path, eight active commands lack handlers, and
five readiness-import commands remain deferred. Test paths in the audit are
source references, not a claim that each event invariant has behavioral proof.

## Remaining source types

The former broad proposal list mixed required work with hypotheses and unresolved policy scope. It is superseded as an implementation plan by the pruned, story-linked [system command list](command-list.md). Five evidence command contracts are present per file: `CaptureEvidenceArtifact`, `RecordEvidenceArtifactInspection`, `IssueArtifactDownload`, `LinkEvidenceSupport`, and `ReleaseEvidenceArtifactQuarantine`, each under `src/Compliance.Common/Features/Evidence/`. All five now have Core handlers; download, support-link and quarantine-release decisions also persist aggregate facts. Development hosts expose bounded personal HTTP upload; production hosts keep it unavailable until production storage is qualified. Scanner-provider qualification, supported content types, download delivery and remaining transport or worker integration stay open in the command and handler checklists. The 130-command plan contains 5 commands explicitly deferred by M0-D04/EN-05; the [handler coverage checklist](handler-coverage-checklist.md) tracks active implementation separately from those deferred imports.

The pruned plan retains 130 feature-backed command requirements, each mapped to a typed command contract; actual-staff revocation reuses its already wired contract and handler. Five readiness-material import commands are explicitly deferred, leaving 125 active handler requirements. Excluded P2 hypotheses, unsettled BO scope, and unvalidated Privacy scope are not counted as required product commands.

The domain-event inventory reconciles all 404 declared source types: 403 accepted facts are mapped to business requirements, and `WorkItemAssignmentHistoryImported` is explicitly excluded as a legacy migration fact. Eight readiness-import events remain deferred and are not current source declarations. The R2-08 `AdvisorHandoff`, T1-01 `ExaminationEngagement`, T1-02 `SystemDescription`, T1-04 `AuditorRequest`, R2-12 evidence-governance roots, T1-05/T3-05 `AuditPackage`, T1-07 examination transition roots, and T2-01/T3-01 `TypeIIPeriod` plus `TypeIIObservationCalendar` have focused RED-to-GREEN behavior tests. All 21 Type I baseline source families are registered and covered by source-contributor tests. Handler coverage is 114 of 125 active commands complete, 3 partial, and 8 without handlers; the three internal T2-07 reaction commands are outside that active public-command count. The Type II proposal and period start preserve exact approved-control expected-evidence descriptions through their plan and occurrence records. Five readiness-import commands remain explicitly deferred. The [handler checklist](handler-coverage-checklist.md) tracks active gaps and prerequisites. Reactors follow handler closure; projectors are the final layer.

## Vertical-slice closure gaps

The query audit found 44 of 272 tracked query contracts without a Core handler or typed HTTP registration. Their contracts and response-view files exist; each still needs an authorized read path and a read model or aggregate source selected by its owning feature. The [query inventory](query-and-view-model-list.md#current-handler-and-http-gaps) names every affected query. Significant-change and incident reads now use tenant-partitioned projections with paged program indexes and restricted-record filtering.

The accepted command plan has 125 active handler requirements: 114 complete, 3 partial, and 8 missing. Five additional readiness-import commands stay deferred by M0-D04/EN-05. Do not treat worker callbacks or scheduler commands as public HTTP routes simply because a command contract exists.

Current source contains 33 reactor types and 78 projector types. Their source counts do not establish behavior coverage: the remaining work is to connect every required query to its authorized read source and projector, close the seven confirmed fact gaps and eight active handler gaps in the aggregate/command audit, and identify each critical cross-aggregate flow that needs a reactor. Use event choreography for follow-up commands and external effects; keep projectors read-only. T2-07 impact-work linking and exact no-change/approved-change description decisions now have internal commands and reactors. Incident approved-successor correlation remains open because the system-description source does not identify incident IDs.

## Validation

The seven existing source inventories (commands, queries, events, aggregates, models, reactors, and projectors) were checked by resolving every linked `.cs` path. The Type I baseline preview composes all 21 required source families, and engagement creation requires an approved readiness entry decision. The focused examination command-handler suite passes 16 tests; all 4,677 non-broker/non-web backend tests pass; the Release build succeeds with zero warnings or errors; and whitespace formatting verification passes. The advisor handoff list authorizes assigned Advisors and scoped guests separately, limits their results to unexpired ready handoffs, and rechecks visibility after reading. Evidence-support links bind an exact artifact and target revision and cannot claim a period outside the artifact's captured coverage; valid reuse stays inside that recorded interval. The close-freeze handler remains partial until the source-backed close-validation command is implemented. Candidate counts are name-level coverage only and do not imply runtime wiring.
