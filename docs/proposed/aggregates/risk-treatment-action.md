# RiskTreatmentAction

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped actionId; `ActionId` on `AddRiskTreatmentAction` is the caller-supplied immutable creation identity (exact immutable creation retry) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One accountable action, completion and independent review lifecycle.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Assessment belongs to RiskEvaluation; risk governance consumes exact risk/source proofs, no live aggregate authority.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AddRiskTreatmentAction](../commands/risk-treatment-action.md#addrisktreatmentaction) | http | ActionId |
| [CancelRiskTreatmentAction](../commands/risk-treatment-action.md#cancelrisktreatmentaction) | http | CommandId |
| [ReviewRiskTreatmentActionCompletion](../commands/risk-treatment-action.md#reviewrisktreatmentactioncompletion) | http | CommandId |
| [ReviseRiskTreatmentAction](../commands/risk-treatment-action.md#reviserisktreatmentaction) | http | CommandId |
| [SubmitRiskTreatmentActionCompletion](../commands/risk-treatment-action.md#submitrisktreatmentactioncompletion) | http | CommandId |
| [AbortRiskTreatmentActionReview](../commands/risk-treatment-action.md#abortrisktreatmentactionreview) | abort | CommandId |
| [CompleteRiskTreatmentActionReview](../commands/risk-treatment-action.md#completerisktreatmentactionreview) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskTreatmentActionAdded](../events/risk-treatment-action.md#risktreatmentactionadded) | bdgrz.risk.treatment_action.added | 2 |
| [RiskTreatmentActionCancelled](../events/risk-treatment-action.md#risktreatmentactioncancelled) | bdgrz.risk.treatment_action.cancelled | 2 |
| [RiskTreatmentActionCompletionReviewAccepted](../events/risk-treatment-action.md#risktreatmentactioncompletionreviewaccepted) | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-accepted | 1 |
| [RiskTreatmentActionCompletionReviewCanceled](../events/risk-treatment-action.md#risktreatmentactioncompletionreviewcanceled) | bdgrz.risk-treatment-action.risk-treatment-action-completion-review-canceled | 1 |
| [RiskTreatmentActionCompletionReviewed](../events/risk-treatment-action.md#risktreatmentactioncompletionreviewed) | bdgrz.risk.treatment_action.completion_reviewed | 2 |
| [RiskTreatmentActionCompletionSubmitted](../events/risk-treatment-action.md#risktreatmentactioncompletionsubmitted) | bdgrz.risk.treatment_action.completion_submitted | 2 |
| [RiskTreatmentActionRevised](../events/risk-treatment-action.md#risktreatmentactionrevised) | bdgrz.risk.treatment_action.revised | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T071](../reactors/risk-action-completion-review-reactor.md#t071) | reaction_or_regular_process_stage | RiskTreatmentAction | EvidenceRequest | AuthorizeEvidenceRequestActionCompletionUse |
| [T072](../reactors/risk-action-completion-review-reactor.md#t072) | reaction_or_regular_process_stage | RiskTreatmentAction | EvidenceRequest | SettleEvidenceRequestActionCompletionUse |
| [T078](../reactors/risk-action-completion-review-reactor.md#t078) | reaction_or_regular_process_stage | RiskTreatmentAction | RiskTreatmentAction | CompleteRiskTreatmentActionReview |
| [A012](../reactors/unassigned-architecture-rows.md#a012) | process_abort_binding | RiskTreatmentAction | RiskTreatmentAction | AbortRiskTreatmentActionReview |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [risk-treatment-action-review-2026-10-29.md](../../product/definitive-aggregate-choreography/risk-treatment-action-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/risk-treatment-action.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
