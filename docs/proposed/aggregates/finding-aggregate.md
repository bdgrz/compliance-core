# FindingAggregate

- Classification: `business_aggregate`
- Source area: `Remediation` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped (tenantId, programId, findingId); `FindingId` on `RaiseFinding` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** Finding description, status and closure admission generation.

**Additional decision rule:** A finding closes only with required closure evidence, verification rationale, program-management permission, and separation of duties; it is the sole authority for these and never infers or grants a separation-of-duties exception or waiver. Gaps, provider coverage gaps, and control evaluation deviations are distinct records: conversion to a Finding preserves source text, provenance, and links, and no record silently becomes or closes another when a linked record changes state.

**Must preserve:** Close only with at least one action, all complete, evidence/rationale present and independent closer unless exact waiver. Closed finding is immutable until reopen.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AddCorrectiveAction](../commands/finding-aggregate.md#addcorrectiveaction) | http | ActionId |
| [CloseFinding](../commands/finding-aggregate.md#closefinding) | http | CommandId |
| [LinkFindingAcceptance](../commands/finding-aggregate.md#linkfindingacceptance) | http | CommandId |
| [RaiseFinding](../commands/finding-aggregate.md#raisefinding) | http | FindingId |
| [RaiseOccurrenceFinding](../commands/finding-aggregate.md#raiseoccurrencefinding) | creation | FindingId |
| [ReopenFinding](../commands/finding-aggregate.md#reopenfinding) | http | CommandId |
| [ReviseFinding](../commands/finding-aggregate.md#revisefinding) | http | CommandId |
| [RaiseEvaluationDeviationFinding](../commands/finding-aggregate.md#raiseevaluationdeviationfinding) | command | CommandId |
| [AbortFindingClosure](../commands/finding-aggregate.md#abortfindingclosure) | abort | CommandId |
| [CompleteFindingClosure](../commands/finding-aggregate.md#completefindingclosure) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CorrectiveActionAdmitted](../events/finding-aggregate.md#correctiveactionadmitted) | bdgrz.finding-aggregate.corrective-action-admitted | 1 |
| [FindingAcceptanceLinkedV2](../events/finding-aggregate.md#findingacceptancelinkedv2) | bdgrz.finding.acceptance.linked | 3 |
| [FindingClosedV2](../events/finding-aggregate.md#findingclosedv2) | bdgrz.finding.closed | 3 |
| [FindingClosureAccepted](../events/finding-aggregate.md#findingclosureaccepted) | bdgrz.finding-aggregate.finding-closure-accepted | 1 |
| [FindingClosureCanceled](../events/finding-aggregate.md#findingclosurecanceled) | bdgrz.finding-aggregate.finding-closure-canceled | 1 |
| [FindingRaisedV2](../events/finding-aggregate.md#findingraisedv2) | bdgrz.finding.raised | 3 |
| [FindingReopenedV2](../events/finding-aggregate.md#findingreopenedv2) | bdgrz.finding.reopened | 3 |
| [FindingRevisedV2](../events/finding-aggregate.md#findingrevisedv2) | bdgrz.finding.revised | 3 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T036](../reactors/corrective-action-materialization-reactor.md#t036) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | MaterializeCorrectiveAction |
| [T037](../reactors/finding-closure-reactor.md#t037) | reaction_or_regular_process_stage | FindingAggregate | FindingAggregate | CompleteFindingClosure |
| [T063](../reactors/finding-closure-reactor.md#t063) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | AuthorizeCorrectiveActionFindingClosureUse |
| [T064](../reactors/finding-closure-reactor.md#t064) | reaction_or_regular_process_stage | FindingAggregate | CorrectiveAction | SettleCorrectiveActionFindingClosureUse |
| [S001](../reactors/control-evaluation-deviation-finding-reactor.md#s001) | business_receiver | ControlEvaluation | FindingAggregate | RaiseEvaluationDeviationFinding |
| [S002](../reactors/control-occurrence-finding-reactor.md#s002) | business_receiver | ControlOccurrence | FindingAggregate | RaiseOccurrenceFinding |
| [A008](../reactors/unassigned-architecture-rows.md#a008) | process_abort_binding | FindingAggregate | FindingAggregate | AbortFindingClosure |

## Product rules

- **D15-03** — The finding remains the authority for required closure evidence, verification rationale, program-management permission, and separation of duties; the work queue never infers or grants a separation-of-duties exception or waiver. Source: [m0-d15-work-queue.md](../../product/decisions/m0-d15-work-queue.md).
- **D23-01** — Gap (readiness unmet rule for an exact assessment and as-of time), ProviderCoverageGap, ControlEvaluationDeviation, and Finding are distinct records; none silently changes into another or closes merely because a linked record changes state, and conversion to a Finding preserves source text, provenance, and links. Source: [m0-d23-assurance-vocabulary.md](../../product/decisions/m0-d23-assurance-vocabulary.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [finding-aggregate-review-2026-10-29.md](../../product/definitive-aggregate-choreography/finding-aggregate-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/finding-aggregate.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
