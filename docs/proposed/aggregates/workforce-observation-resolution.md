# WorkforceObservationResolution

- Classification: `business_aggregate`
- Source area: `Workforce` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + ObservationId, with immutable source observation identity. Selected CommandId binds the exact normalized resolution/note and authorized actor.

**Invariant:** One observation may receive one final attributed resolved/dismissed decision; changed resolution intent conflicts.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ResolveWorkforceObservation](../commands/workforce-observation-resolution.md#resolveworkforceobservation) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkforceObservationResolved](../events/workforce-observation-resolution.md#workforceobservationresolved) | bdgrz.workforce.observation.resolved | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/workforce-observation-resolution.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
