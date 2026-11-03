# ApplicationImportBatch

- Classification: `business_aggregate`
- Source area: `Applications` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant + exact validated SourceKey/namespace + BatchId under unchanged ApplicationImportBatch.BatchIdFor and selected SubmissionId/BatchKey bindings. No case folding or source-key normalization is introduced.

**Invariant:** Frozen source rows, row outcomes, per-batch execution and terminal disposition.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Freeze complete rows and expected revisions. No partial batch visibility. Target owner replay uses accepted native facts without live coordinator lookup. Staged observation and execution share one ApplicationImportBatch revision; old ImportBatch is a cutover predecessor only.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcceptApplicationImport](../commands/application-import-batch.md#acceptapplicationimport) | http | CommandId |
| [CancelApplicationImport](../commands/application-import-batch.md#cancelapplicationimport) | http | CommandId |
| [RecordApplicationImportRowChoice](../commands/application-import-batch.md#recordapplicationimportrowchoice) | http | CommandId |
| [ExecuteApplicationImport](../commands/application-import-batch.md#executeapplicationimport) | command | CommandId |
| [FreezeApplicationImportOmissionProposal](../commands/application-import-batch.md#freezeapplicationimportomissionproposal) | http | CommandId |
| [StageApplicationImport](../commands/application-import-batch.md#stageapplicationimport) | http | SubmissionId |
| [CommitApplicationImportBatch](../commands/application-import-batch.md#commitapplicationimportbatch) | receiver | CommandId |
| [CompleteApplicationImportCancellation](../commands/application-import-batch.md#completeapplicationimportcancellation) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ApplicationImportCanceled](../events/application-import-batch.md#applicationimportcanceled) | bdgrz.application_import.canceled | 2 |
| [ApplicationImportCancellationAccepted](../events/application-import-batch.md#applicationimportcancellationaccepted) | bdgrz.application_import.cancellation_accepted | 1 |
| [ApplicationImportCommitted](../events/application-import-batch.md#applicationimportcommitted) | bdgrz.application_import.committed | 2 |
| [ApplicationImportExecutionAuthorized](../events/application-import-batch.md#applicationimportexecutionauthorized) | bdgrz.application_import.execution_authorized | 1 |
| [ApplicationImportFailed](../events/application-import-batch.md#applicationimportfailed) | bdgrz.application_import.failed | 2 |
| [ApplicationImportPlanAccepted](../events/application-import-batch.md#applicationimportplanaccepted) | bdgrz.application_import.plan_sealed | 2 |
| [ApplicationImportRetirementProposalFinalized](../events/application-import-batch.md#applicationimportretirementproposalfinalized) | bdgrz.application_import.retirement_proposal_sealed | 2 |
| [ApplicationImportRowChoiceRecorded](../events/application-import-batch.md#applicationimportrowchoicerecorded) | bdgrz.application_import.row_correlated | 2 |
| [ApplicationImportPrepared](../events/application-import-batch.md#applicationimportprepared) | bdgrz.application_import.staged | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T014](../reactors/import-source-admission-reactor.md#t014) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportSource | ClaimApplicationImportSource |
| [T015](../reactors/import-source-admission-reactor.md#t015) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ReserveApplicationImportTarget |
| [T016](../reactors/import-source-admission-reactor.md#t016) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ApplyApplicationImportChange |
| [T017](../reactors/import-source-admission-reactor.md#t017) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportBatch | CommitApplicationImportBatch |
| [T018](../reactors/import-cancellation-reactor.md#t018) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | BlockCanceledApplicationImportChanges |
| [T019](../reactors/import-cancellation-reactor.md#t019) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportBatch | CompleteApplicationImportCancellation |
| [T020](../reactors/import-source-admission-reactor.md#t020) | reaction_or_regular_process_stage | ApplicationImportBatch | ApplicationImportSource | ReleaseApplicationImportSource |
| [T021](../reactors/import-source-admission-reactor.md#t021) | reaction_or_regular_process_stage | ApplicationImportBatch | DeclaredApplication | ReleaseApplicationImportTarget |
| [N000](../reactors/import-source-admission-reactor.md#n000) | nested_process_stage | ApplicationImportBatch | ApplicationImportBatch | ExecuteApplicationImport |
| [A013](../reactors/unassigned-architecture-rows.md#a013) | process_abort_binding | ApplicationImportBatch | ApplicationImportBatch | CancelApplicationImport |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [application-import-batch-review-2026-10-29.md](../../product/definitive-aggregate-choreography/application-import-batch-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/application-import-batch.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
