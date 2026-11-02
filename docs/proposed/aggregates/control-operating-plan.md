# ControlOperatingPlan

- Classification: `business_aggregate`
- Source area: `Operations` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Deterministic (tenantId, programId, controlId) stream per tenant; `PlanVersionId` identifies each proposal operation on the deterministic tuple stream; mutations bind CommandId to the tuple and the expected revision.

**Invariant:** One independently approved, non-overlapping plan version line for a control.

**Additional decision rule:** No additional write-time refusals beyond the invariant are traced to this owner; D19-05 (annual policy review one occurrence per policy, annual vendor review one per material provider) is reassigned to ControlOccurrence.

**Must preserve:** Occurrence facts bind exact frozen plan and evidence requirements; one business window has one identity across all generators.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ApproveControlOperatingPlan](../commands/control-operating-plan.md#approvecontroloperatingplan) | http | CommandId |
| [ProposeControlOperatingPlan](../commands/control-operating-plan.md#proposecontroloperatingplan) | http | PlanVersionId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlOperatingPlanApproved](../events/control-operating-plan.md#controloperatingplanapproved) | bdgrz.control.operating_plan.approved | 2 |
| [ControlOperatingPlanProposed](../events/control-operating-plan.md#controloperatingplanproposed) | bdgrz.control.operating_plan.proposed | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [control-operating-plan-review-2026-10-29.md](../../product/definitive-aggregate-choreography/control-operating-plan-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/control-operating-plan.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
