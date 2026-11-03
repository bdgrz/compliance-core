# RiskMethod

- Classification: `business_aggregate`
- Source area: `Risks` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Existing tenant-scoped risk-method identity derived from ProgramId by the unchanged UUIDv5 algorithm. Selected CommandId identifies publication of one exact method/version intent.

**Invariant:** The program method advances sequentially by expected published version and each immutable definition has complete valid 5x5 scales and permitted appetite.

**Additional decision rule:** A method is qualitative 5x5 with five-level likelihood and impact scales, a nullable appetite threshold when set between 1 and 25 residual score, and a P1Y reassessment interval.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [PublishRiskMethodVersion](../commands/risk-method.md#publishriskmethodversion) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RiskMethodVersionPublished](../events/risk-method.md#riskmethodversionpublished) | bdgrz.risk.method.version_published | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S036](../reactors/risk-method-reassessment-reactor.md#s036) | business_receiver | RiskMethod | RiskGovernance | RaiseRiskReassessmentTriggers |

## Product rules

- **D10-01** — A risk method is versioned per program, uses a qualitative 5x5 kind with five-level likelihood and impact scales, and carries a nullable appetite threshold (1 to 25 residual score) and a one-year reassessment interval. Decision: [m0-d10-risk-method.md](../decisions/m0-d10-risk-method.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/risk-method.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
