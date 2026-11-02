# RiskGovernance

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped riskId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One risk’s governance and reassessment obligations.

**Additional decision rule:** A reassessment trigger is one of significant change, incident, boundary change, new material vendor or expired acceptance, and a risk with no full reassessment for 12 months is flagged due again.

**Must preserve:** Assessment belongs to RiskEvaluation; risk governance consumes exact risk/source proofs, no live aggregate authority.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignRiskOwner](../commands/risk-governance.md#assignriskowner) | http | CommandId |
| [RaiseRiskReassessmentTriggers](../commands/risk-governance.md#raiseriskreassessmenttriggers) | command | CommandId |
| [RecordRiskReassessmentObligation](../commands/risk-governance.md#recordriskreassessmentobligation) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskOwnerAssigned](../events/risk-governance.md#riskownerassigned) | bdgrz.risk.owner.assigned | 2 |
| [RiskReassessmentTriggered](../events/risk-governance.md#riskreassessmenttriggered) | bdgrz.risk.reassessment.triggered | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S036](../reactors/risk-method-reassessment-reactor.md#s036) | business_receiver | RiskMethod | RiskGovernance | RaiseRiskReassessmentTriggers |
| [S037](../reactors/boundary-risk-reassessment-reactor.md#s037) | business_receiver | SystemBoundary | RiskGovernance | RaiseRiskReassessmentTriggers |
| [X009](../reactors/risk-evaluation-reactor.md#x009) | planned_reaction | RiskEvaluation | RiskGovernance | RecordRiskReassessmentObligation |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/risk-governance.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
