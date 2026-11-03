# EmailAddress

- Classification: `business_aggregate`
- Source area: `UserIdentities` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of the normalized email address. ChallengeId and selected CommandId are distinct stable operation identities bound to this exact address, owner and recovery episode.

**Invariant:** A normalized address reserves exactly one platform user; verification requires its current challenge; a claimed recovery binds one old/replacement pair.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CompleteEmailChallenge](../commands/email-address.md#completeemailchallenge) | http | ChallengeId |
| [IssueEmailChallenge](../commands/email-address.md#issueemailchallenge) | http | ChallengeId |
| [ReserveEmail](../commands/email-address.md#reserveemail) | http | CommandId |
| [CompleteIdentityRecovery](../commands/email-address.md#completeidentityrecovery) | http | ChallengeId |
| [StartIdentityRecovery](../commands/email-address.md#startidentityrecovery) | http | ChallengeId |
| [CompleteEmailRecoveryChallenge](../commands/email-address.md#completeemailrecoverychallenge) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [EmailAddressReserved](../events/email-address.md#emailaddressreserved) | bdgrz.email-address.reserved | 1 |
| [EmailAddressVerified](../events/email-address.md#emailaddressverified) | bdgrz.email-address.verified | 1 |
| [EmailChallengeIssued](../events/email-address.md#emailchallengeissued) | bdgrz.email-address.challenge-issued | 1 |
| [IdentityRecoveryChallengeClaimed](../events/email-address.md#identityrecoverychallengeclaimed) | bdgrz.identity-recovery.challenge-claimed | 1 |
| [IdentityRecoveryChallengeCompleted](../events/email-address.md#identityrecoverychallengecompleted) | bdgrz.identity-recovery.challenge-completed | 1 |
| [IdentityRecoveryChallengeIssued](../events/email-address.md#identityrecoverychallengeissued) | bdgrz.identity-recovery.challenge-issued | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T056](../reactors/identity-recovery-acceptance-reactor.md#t056) | reaction_or_regular_process_stage | EmailAddress | IdentityRecovery | PrepareAccountRecovery |
| [T058](../reactors/identity-recovery-acceptance-reactor.md#t058) | reaction_or_regular_process_stage | IdentityRecovery | EmailAddress | CompleteEmailRecoveryChallenge |
| [T115](../reactors/email-challenge-delivery-reactor.md#t115) | reaction_or_regular_process_stage | EmailAddress | EmailChallengeDeliveryLedger | DeliverEmailChallenge |
| [T116](../reactors/email-challenge-delivery-reactor.md#t116) | reaction_or_regular_process_stage | EmailAddress | EmailChallengeDeliveryLedger | DeliverRecoveryChallenge |
| [S035](../reactors/email-reservation-reactor.md#s035) | business_receiver | UserIdentity | EmailAddress | ReserveEmail |
| [N001](../reactors/email-challenge-delivery-reactor.md#n001) | nested_process_stage | EmailAddress | EmailChallengeDeliveryLedger | RecordEmailChallengeDeliverySent |
| [N002](../reactors/email-challenge-delivery-reactor.md#n002) | nested_process_stage | EmailAddress | EmailChallengeDeliveryLedger | RecordEmailChallengeDeliveryFailed |
| [A017](../reactors/unassigned-architecture-rows.md#a017) | process_abort_binding | EmailAddress | IdentityRecovery | RejectIdentityRecoveryPreparation |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [email-address-review-2026-10-29.md](../../product/definitive-aggregate-choreography/email-address-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/email-address.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
