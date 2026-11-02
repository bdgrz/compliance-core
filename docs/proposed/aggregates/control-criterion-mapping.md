# ControlCriterionMapping

- Classification: `business_aggregate`
- Source area: `ControlMappings` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** (programId, controlId, editionId, criterionId)

**Invariant:** One mapping tuple proposal and immutable approved successor line.

**Additional decision rule:** A mapping references a criterion only by edition id and criterion id and rejects any criterion text; each approved successor in the line references the same tuple.

**Must preserve:** Independent review approves exact pending proposal and supersedes only its predecessor.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ProposeControlCriterionMapping](../commands/control-criterion-mapping.md#proposecontrolcriterionmapping) | http | MappingId |
| [RetireControlCriterionMapping](../commands/control-criterion-mapping.md#retirecontrolcriterionmapping) | http | CommandId |
| [ReviewControlCriterionMapping](../commands/control-criterion-mapping.md#reviewcontrolcriterionmapping) | http | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [ControlCriterionMappingProposed](../events/control-criterion-mapping.md#controlcriterionmappingproposed) | bdgrz.control_mapping.proposed | 3 |
| [ControlCriterionMappingRetired](../events/control-criterion-mapping.md#controlcriterionmappingretired) | bdgrz.control_mapping.retired | 3 |
| [ControlCriterionMappingReviewed](../events/control-criterion-mapping.md#controlcriterionmappingreviewed) | bdgrz.control_mapping.reviewed | 3 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D02-07** — A control mapping references a criterion by edition identifier and criterion identifier, never by criterion text. Source: [m0-d02-criteria-content.md](../../product/decisions/m0-d02-criteria-content.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [control-criterion-mapping-review-2026-10-29.md](../../product/definitive-aggregate-choreography/control-criterion-mapping-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/control-criterion-mapping.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
