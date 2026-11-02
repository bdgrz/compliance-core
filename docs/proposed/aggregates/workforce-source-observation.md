# WorkforceSourceObservation

- Classification: `business_aggregate`
- Source area: `Workforce` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of tenant + length-prefixed source kind/system/record/revision tuple. Supplied ObservationId must equal the unchanged IdFor result; selected mutations use CommandId with the exact source tuple.

**Invariant:** One immutable external source revision has one target/fact correlation and at most one final attributed reconciliation decision tied to exact canonical revision.

**Additional decision rule:** An HRIS observation is authoritative for people, employment status and manager while an IdP observation only corroborates; a disagreement is surfaced as a conflict resolved only by one attributable accepted or dismissed decision with a note of 1 to 1000 characters, never silently.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ReconcileWorkforceSourceObservation](../commands/workforce-source-observation.md#reconcileworkforcesourceobservation) | command | CommandId |
| [RecordWorkforceSourceObservation](../commands/workforce-source-observation.md#recordworkforcesourceobservation) | http | ObservationId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [WorkforceSourceObserved](../events/workforce-source-observation.md#workforcesourceobserved) | bdgrz.workforce.source.observed | 1 |
| [WorkforceSourceReconciled](../events/workforce-source-observation.md#workforcesourcereconciled) | bdgrz.workforce.source.reconciled | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D06-01** — An HRIS roster export is authoritative for people, employment status, and manager, while the identity-provider directory only corroborates it; a disagreement is surfaced as a conflict requiring an attributable reconciliation decision and is never silently resolved. Source: [m0-d06-workforce-source.md](../../product/decisions/m0-d06-workforce-source.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/workforce-source-observation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
