# ReadinessGapPlan

- Classification: `business_aggregate`
- Source area: `Readiness` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped tenant / (programId, gapId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One stable gap action/owner/date plan and independent annotations; admission/seal generation fences changes during a readiness decision.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** No projected latest assessment or gap list can authorize entry; all exceptions/required plans acknowledged.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AnnotateReadinessGap](../commands/readiness-gap-plan.md#annotatereadinessgap) | http | CommandId |
| [PlanReadinessGap](../commands/readiness-gap-plan.md#planreadinessgap) | http | CommandId |
| [AuthorizeReadinessGapPlanReadinessDecision](../commands/readiness-gap-plan.md#authorizereadinessgapplanreadinessdecision) | receiver | CommandId |
| [CloseReadinessGapPlanReadinessDecisionAuthorization](../commands/readiness-gap-plan.md#closereadinessgapplanreadinessdecisionauthorization) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ReadinessGapAnnotated](../events/readiness-gap-plan.md#readinessgapannotated) | bdgrz.readiness.gap.annotated | 1 |
| [ReadinessGapPlanReadinessDecisionAuthorized](../events/readiness-gap-plan.md#readinessgapplanreadinessdecisionauthorized) | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-authorized | 1 |
| [ReadinessGapPlanReadinessDecisionAuthorizationClosed](../events/readiness-gap-plan.md#readinessgapplanreadinessdecisionauthorizationclosed) | bdgrz.readiness-gap-plan.readiness-gap-plan-readiness-decision-use-settled | 1 |
| [ReadinessGapPlanned](../events/readiness-gap-plan.md#readinessgapplanned) | bdgrz.readiness.gap.planned | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T065](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t065) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessGapPlan | AuthorizeReadinessGapPlanReadinessDecision |
| [T066](../reactors/record-program-readiness-and-type-i-decisions-reactor.md#t066) | reaction_or_regular_process_stage | ProgramReadiness | ReadinessGapPlan | CloseReadinessGapPlanReadinessDecisionAuthorization |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/readiness-gap-plan.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
