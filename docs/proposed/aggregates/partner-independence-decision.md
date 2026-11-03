# PartnerIndependenceDecision

- Classification: `immutable_business_artifact_or_decision`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / evaluationId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One personal partner evaluation bound to exact engagement revision, complete service epoch and ratified-rule digest.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Never reactivate a permanent independence commitment from reevaluation; acceptance binds exact service, draft, acknowledgement and ratified-rule proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RecordPartnerIndependenceEvaluation](../commands/partner-independence-decision.md#recordpartnerindependenceevaluation) | receiver | CommandId |
| [SubmitPartnerIndependenceEvaluation](../commands/partner-independence-decision.md#submitpartnerindependenceevaluation) | http | EvaluationId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PartnerIndependenceEvaluationRecorded](../events/partner-independence-decision.md#partnerindependenceevaluationrecorded) | bdgrz.service-engagement.partner-evaluation.recorded | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T109](../reactors/engagement-personal-decision-reactor.md#t109) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PartnerIndependenceDecision | RecordPartnerIndependenceEvaluation |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [partner-independence-decision-review-2026-10-29.md](../../product/definitive-aggregate-choreography/partner-independence-decision-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/partner-independence-decision.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
