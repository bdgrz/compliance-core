# RiskEvaluation

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant / risk-evaluations / RiskId, with immutable ProgramId and exact method/risk version. Selected CommandId binds assessment/acceptance intent and expected evaluation revision.

**Invariant:** Assessment phases follow inherent/current method; residual requires treatment and mitigate admission proof; acceptance must name current residual and accept treatment, satisfy score/appetite/authority/expiry, and be independent from assessor/owner unless exactly waived.

**Additional decision rule:** An assessment records inherent or residual phase, likelihood 1 to 5 and impact 1 to 5 against a specific method version, with the score derived as likelihood times impact and never supplied. A treatment is one of mitigate, accept, transfer or avoid and carries a rationale. A residual score above the method's appetite threshold is above appetite, and while the threshold is null every risk is treated as above appetite; the Compliance Lead may accept only at or below a non-null threshold and anything else requires an executive approver. Acceptance expiry must be after acceptance and no later than 12 months after it, after which the risk returns to reassessment, and acceptance does not close findings or count as remediation. An externally sourced IncidentReference (source ID, occurrence time, summary, provenance) may trigger reassessment but never rewrites earlier risk history.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcceptRisk](../commands/risk-evaluation.md#acceptrisk) | http | CommandId |
| [ChooseRiskTreatment](../commands/risk-evaluation.md#chooserisktreatment) | command | CommandId |
| [RecordRiskAssessment](../commands/risk-evaluation.md#recordriskassessment) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskAccepted](../events/risk-evaluation.md#riskaccepted) | bdgrz.risk.accepted | 2 |
| [RiskAssessmentRecorded](../events/risk-evaluation.md#riskassessmentrecorded) | bdgrz.risk.assessment.recorded | 2 |
| [RiskTreatmentChosen](../events/risk-evaluation.md#risktreatmentchosen) | bdgrz.risk.treatment.chosen | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [X009](../reactors/request-risk-reassessment-when-risk-changes-reactor.md#x009) | planned_reaction | RiskEvaluation | RiskGovernance | RequireRiskReassessment |

## Product rules

- **D10-02** — A risk assessment records inherent or residual phase, likelihood 1 to 5, impact 1 to 5, assessor, and time against a specific method version; the score is derived as likelihood times impact and never user-entered. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-03** — A residual risk assessment requires at least one control risk treatment or a non-mitigate treatment. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-04** — A risk treatment is one of mitigate, accept, transfer, or avoid and carries a rationale. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-05** — A residual score above the method version's appetite threshold is above appetite, and while the threshold is unset every risk is treated as above appetite for acceptance. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-06** — A risk acceptance must reference the residual assessment, be time-bounded with expiry no later than 12 months after acceptance, and on expiry the risk returns to reassessment. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-07** — The Compliance Lead may accept a risk only when its residual score is at or below a non-null appetite threshold; accepting a risk above appetite, or any acceptance while appetite is null, requires an executive approver. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D10-08** — A risk acceptance is invalid when the approver is the risk owner unless a separation-of-duties exception exists. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).
- **D22-07** — Risk assessment owns the externally sourced IncidentReference (source ID, occurrence time, summary, provenance); it can trigger reassessment but is not an incident workflow, and the later governed incident may correlate it without rewriting earlier risk history. Decision: [m0-d22-canonical-ownership.md](../decisions/m0-d22-canonical-ownership.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/risk-evaluation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
