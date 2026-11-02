# CriterionApplicability

- Classification: `business_aggregate`
- Source area: `ControlMappings` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Deterministic (programId, editionId, criterionId) stream per tenant; `DecisionId` identifies each applicability decision on the deterministic tuple stream; mutations bind CommandId to the tuple and the expected revision.

**Invariant:** One applicability decision lineage.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Only independent accepted review can make a criterion not applicable; withdrawal restores coverage.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ProposeCriterionNotApplicable](../commands/criterion-applicability.md#proposecriterionnotapplicable) | http | DecisionId |
| [ReviewCriterionApplicability](../commands/criterion-applicability.md#reviewcriterionapplicability) | http | CommandId |
| [WithdrawCriterionNotApplicable](../commands/criterion-applicability.md#withdrawcriterionnotapplicable) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CriterionApplicabilityReviewed](../events/criterion-applicability.md#criterionapplicabilityreviewed) | bdgrz.criterion_applicability.reviewed | 3 |
| [CriterionNotApplicableProposed](../events/criterion-applicability.md#criterionnotapplicableproposed) | bdgrz.criterion_applicability.not_applicable_proposed | 3 |
| [CriterionNotApplicableWithdrawn](../events/criterion-applicability.md#criterionnotapplicablewithdrawn) | bdgrz.criterion_applicability.not_applicable_withdrawn | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [criterion-applicability-review-2026-10-29.md](../../product/definitive-aggregate-choreography/criterion-applicability-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/criterion-applicability.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
