# TrainingRequirement

- Classification: `business_aggregate`
- Source area: `PolicyDistribution` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** (tenantId, programId, normalizedRequirementId)

**Invariant:** One normalized training requirement creation and version line.

**Additional decision rule:** A requirement names the course, an audience rule and the cadence hire_within_30_days_then_annual, with delivery source manual or lms_export.

**Must preserve:** Original creation identity remains recognizable after revisions; each revision uses expected version and valid audience/source.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DefineTrainingRequirement](../commands/training-requirement.md#definetrainingrequirement) | http | RequirementId |
| [ReviseTrainingRequirement](../commands/training-requirement.md#revisetrainingrequirement) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TrainingRequirementDefined](../events/training-requirement.md#trainingrequirementdefined) | bdgrz.training_requirement.defined | 2 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D12-10** — A TrainingRequirement names the course, an audience rule, and a cadence of within 30 days of hire then annually; the delivery source is manual or lms_export. Source: [m0-d12-policy-acknowledgement-training.md](../../product/decisions/m0-d12-policy-acknowledgement-training.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [training-requirement-review-2026-10-29.md](../../product/definitive-aggregate-choreography/training-requirement-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/training-requirement.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
