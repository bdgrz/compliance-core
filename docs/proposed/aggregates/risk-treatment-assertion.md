# RiskTreatmentAssertion

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped (riskId, controlId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** One pending/accepted assertion line per risk/control.

**Additional decision rule:** The assertion that a specific control version treats a specific risk is proposed with a rationale, accepted or rejected by a review, and retired only by naming the exact accepted treatment, so a control never silently acquires coverage; a line holds one pending proposal and never reuses a treatment id.

**Must preserve:** Assessment belongs to RiskEvaluation; risk governance consumes exact risk/source proofs, no live aggregate authority.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ProposeRiskControlTreatment](../commands/risk-treatment-assertion.md#proposeriskcontroltreatment) | http | CommandId |
| [RetireRiskControlTreatment](../commands/risk-treatment-assertion.md#retireriskcontroltreatment) | http | CommandId |
| [ReviewRiskControlTreatment](../commands/risk-treatment-assertion.md#reviewriskcontroltreatment) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskControlTreatmentProposed](../events/risk-treatment-assertion.md#riskcontroltreatmentproposed) | bdgrz.risk.control_treatment.proposed | 2 |
| [RiskControlTreatmentRetired](../events/risk-treatment-assertion.md#riskcontroltreatmentretired) | bdgrz.risk.control_treatment.retired | 2 |
| [RiskControlTreatmentReviewed](../events/risk-treatment-assertion.md#riskcontroltreatmentreviewed) | bdgrz.risk.control_treatment.reviewed | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D22-08** — The control-to-risk relationship is a versioned assertion owned by risk treatment that a specific control version treats a specific risk, with rationale and review; a control does not silently acquire risk coverage. Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/risk-treatment-assertion.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
