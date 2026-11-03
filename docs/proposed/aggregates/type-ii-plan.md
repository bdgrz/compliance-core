# TypeIIPlan

- Classification: `business_aggregate`
- Source area: `Examinations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / type-ii-plans / PlanId, with immutable ProgramId. PlanId identifies creation; selected mutations use CommandId with exact plan version/source/decision bindings.

**Invariant:** Only current proposed content with exact carried-gap acknowledgements receives an independent review; executive approval requires review and three distinct roles, after which plan is immutable.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveTypeIIOperatingPlan](../commands/type-ii-plan.md#approvetypeiioperatingplan) | command | CommandId |
| [ProposeTypeIIOperatingPlan](../commands/type-ii-plan.md#proposetypeiioperatingplan) | http | PlanId |
| [ReviewTypeIIOperatingPlan](../commands/type-ii-plan.md#reviewtypeiioperatingplan) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TypeIIPlanApproved](../events/type-ii-plan.md#typeiiplanapproved) | bdgrz.examinations.type-i-i-plan-approved | 2 |
| [TypeIIPlanProposed](../events/type-ii-plan.md#typeiiplanproposed) | bdgrz.examinations.type-i-i-plan-proposed | 2 |
| [TypeIIPlanReviewed](../events/type-ii-plan.md#typeiiplanreviewed) | bdgrz.examinations.type-i-i-plan-reviewed | 1 |
| [TypeIIPlanRevised](../events/type-ii-plan.md#typeiiplanrevised) | bdgrz.examinations.type-i-i-plan-revised | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/type-ii-plan.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
