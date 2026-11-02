# PracticeIndependenceCommitment

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Tenant-scoped tenant / (subjectKind, subjectId); the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Permanent practice binding for subjectKind=staff or canonical_user; acquire both user and staff subjects in canonical stream-key order. No release/revoke command exists.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Never reactivate a permanent independence commitment from reevaluation; acceptance binds exact service, draft, acknowledgement and ratified-rule proofs.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ReservePracticeIndependence](../commands/practice-independence-commitment.md#reservepracticeindependence) | receiver | CommandId |
| [CommitPracticeIndependence](../commands/practice-independence-commitment.md#commitpracticeindependence) | receiver | CommandId |
| [SettlePracticeIndependenceReservation](../commands/practice-independence-commitment.md#settlepracticeindependencereservation) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [PracticeIndependenceCommitted](../events/practice-independence-commitment.md#practiceindependencecommitted) | bdgrz.practice-independence-commitment.practice-independence-committed | 1 |
| [PracticeIndependenceReservationSettled](../events/practice-independence-commitment.md#practiceindependencereservationsettled) | bdgrz.practice-independence-commitment.practice-independence-reservation-settled | 1 |
| [PracticeIndependenceReserved](../events/practice-independence-commitment.md#practiceindependencereserved) | bdgrz.practice-independence-commitment.practice-independence-reserved | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T005](../reactors/engagement-acceptance-reactor.md#t005) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | CommitPracticeIndependence |
| [T083](../reactors/engagement-acceptance-reactor.md#t083) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | ReservePracticeIndependence |
| [T085](../reactors/engagement-acceptance-reactor.md#t085) | reaction_or_regular_process_stage | ServiceEngagementIndependence | PracticeIndependenceCommitment | SettlePracticeIndependenceReservation |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [practice-independence-commitment-review-2026-10-29.md](../../product/definitive-aggregate-choreography/practice-independence-commitment-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/practice-independence-commitment.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
