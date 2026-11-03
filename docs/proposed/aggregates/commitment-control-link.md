# CommitmentControlLink

- Classification: `business_aggregate`
- Source area: `[Placeholder: source capability area is not recorded]` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Deterministic tenant + program + commitment identity + control identity tuple (same scheme as RiskTreatmentAssertion); `LinkId` must equal that tuple; later commands use CommandId with the exact commitment version and control version.

**Invariant:** One commitment-to-control link has one pending/accepted version line: each accepted version binds an exact commitment version to an exact control version with rationale, a successor needs independent review, and a retired link stays terminal (M0-D09 D09-09). Neither CommitmentDraft nor ControlDraft owns the other side's version list.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** [Placeholder: replay rule not recorded.]

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ProposeCommitmentControlLink](../commands/commitment-control-link.md#proposecommitmentcontrollink) | creation | LinkId |
| [ReviewCommitmentControlLink](../commands/commitment-control-link.md#reviewcommitmentcontrollink) | command | CommandId |
| [RetireCommitmentControlLink](../commands/commitment-control-link.md#retirecommitmentcontrollink) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [CommitmentControlLinkProposed](../events/commitment-control-link.md#commitmentcontrollinkproposed) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [CommitmentControlLinkRetired](../events/commitment-control-link.md#commitmentcontrollinkretired) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |
| [CommitmentControlLinkReviewed](../events/commitment-control-link.md#commitmentcontrollinkreviewed) | [Placeholder: discriminator not recorded] | [Placeholder: value not recorded] |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

- **D09-09** — Commitments and controls are linked many-to-many through versioned commitment-control links. Decision: [m0-d09-commitments.md](../decisions/m0-d09-commitments.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: baseline source not recorded]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/commitment-control-link.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `PLANNED_NO_SOURCE_TYPE`.
