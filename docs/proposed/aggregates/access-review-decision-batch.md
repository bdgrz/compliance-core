# AccessReviewDecisionBatch

- Classification: `business_aggregate`
- Source area: `AccessReviews` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / (campaignId, decisionBatchId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One immutable exact preview/item selection and shared-rationale bulk decision, item reservations and one all-item publication decision. Partial item application stays unpublished.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Keep reviewer and remediation owner distinct per item unless exact waiver; no stale projected summary may satisfy completion.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CancelAccessReviewDecisionBatch](../commands/access-review-decision-batch.md#cancelaccessreviewdecisionbatch) | abort | CommandId |
| [PublishAccessReviewDecisionBatch](../commands/access-review-decision-batch.md#publishaccessreviewdecisionbatch) | receiver | CommandId |
| [CompleteAccessReviewDecisionBatchCancellation](../commands/access-review-decision-batch.md#completeaccessreviewdecisionbatchcancellation) | receiver | CommandId |
| [RecordAccessReviewDecisionBatch](../commands/access-review-decision-batch.md#recordaccessreviewdecisionbatch) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [AccessReviewDecisionBatchAccepted](../events/access-review-decision-batch.md#accessreviewdecisionbatchaccepted) | bdgrz.access-review-decision-batch.access-review-decision-batch-accepted | 1 |
| [AccessReviewDecisionBatchCanceled](../events/access-review-decision-batch.md#accessreviewdecisionbatchcanceled) | bdgrz.access-review-decision-batch.access-review-decision-batch-canceled | 1 |
| [AccessReviewDecisionBatchCancellationAccepted](../events/access-review-decision-batch.md#accessreviewdecisionbatchcancellationaccepted) | bdgrz.access-review-decision-batch.access-review-decision-batch-cancellation-accepted | 1 |
| [AccessReviewDecisionBatchPublished](../events/access-review-decision-batch.md#accessreviewdecisionbatchpublished) | bdgrz.access-review-decision-batch.access-review-decision-batch-published | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T009](../reactors/access-review-bulk-admission-reactor.md#t009) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | ReserveAccessReviewItemDecision |
| [T010](../reactors/access-review-bulk-admission-reactor.md#t010) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | ApplyAccessReviewItemDecision |
| [T011](../reactors/access-review-bulk-admission-reactor.md#t011) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewDecisionBatch | PublishAccessReviewDecisionBatch |
| [T012](../reactors/access-review-bulk-cancellation-reactor.md#t012) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | CancelAccessReviewItemDecision |
| [T079](../reactors/access-review-bulk-cancellation-completion-reactor.md#t079) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewDecisionBatch | CompleteAccessReviewDecisionBatchCancellation |
| [T080](../reactors/access-review-bulk-admission-reactor.md#t080) | reaction_or_regular_process_stage | AccessReviewDecisionBatch | AccessReviewItem | CloseAccessReviewItemDecisionAuthorization |
| [T086](../reactors/access-review-bulk-admission-reactor.md#t086) | reaction_or_regular_process_stage | AccessReviewCampaign | AccessReviewDecisionBatch | RecordAccessReviewDecisionBatch |
| [A005](../reactors/unassigned-architecture-rows.md#a005) | process_abort_binding | AccessReviewCampaign | AccessReviewDecisionBatch | CancelAccessReviewDecisionBatch |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [access-review-decision-batch-review-2026-10-29.md](../../product/definitive-aggregate-choreography/access-review-decision-batch-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/access-review-decision-batch.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
