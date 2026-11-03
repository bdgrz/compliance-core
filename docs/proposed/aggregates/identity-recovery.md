# IdentityRecovery

- Classification: `business_aggregate`
- Source area: `[Placeholder: source capability area is not recorded]` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped platform / challengeId; the identity is established by the admitting owner or reactor named in the choreography, never by a caller request key; mutations bind CommandId to this exact identity.

**Invariant:** Immutable both-proof-verified candidate and preparation; complete protection proofs precede accepted exact swap. Preparation can reject/fence; accepted swap completes forward. Exact account, old/replacement identities and actor/proof provenance retained without bearer tokens.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [RejectIdentityRecoveryPreparation](../commands/identity-recovery.md#rejectidentityrecoverypreparation) | command | CommandId |
| [CompleteAcceptedIdentityRecovery](../commands/identity-recovery.md#completeacceptedidentityrecovery) | receiver | CommandId |
| [PrepareAccountRecovery](../commands/identity-recovery.md#prepareaccountrecovery) | receiver | CommandId |
| [AcceptProtectedIdentityRecovery](../commands/identity-recovery.md#acceptprotectedidentityrecovery) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [IdentityRecoveryAccepted](../events/identity-recovery.md#identityrecoveryaccepted) | bdgrz.identity-recovery.identity-recovery-accepted | 1 |
| [IdentityRecoveryCompleted](../events/identity-recovery.md#identityrecoverycompleted) | bdgrz.identity-recovery.identity-recovery-completed | 1 |
| [IdentityRecoveryPreparationRejected](../events/identity-recovery.md#identityrecoverypreparationrejected) | bdgrz.identity-recovery.identity-recovery-preparation-rejected | 1 |
| [IdentityRecoveryPrepared](../events/identity-recovery.md#identityrecoveryprepared) | bdgrz.identity-recovery.identity-recovery-prepared | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T041](../reactors/complete-account-recovery-reactor.md#t041) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | ProtectRecoveryIdentity |
| [T042](../reactors/complete-account-recovery-reactor.md#t042) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | RegisterRecoveredIdentity |
| [T043](../reactors/complete-account-recovery-reactor.md#t043) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | RevokeRecoveredOldIdentity |
| [T044](../reactors/complete-account-recovery-reactor.md#t044) | reaction_or_regular_process_stage | IdentityRecovery | IdentityRecovery | CompleteAcceptedIdentityRecovery |
| [T045](../reactors/complete-account-recovery-reactor.md#t045) | reaction_or_regular_process_stage | IdentityRecovery | IdentityRecoveryNoticeProcess | SendIdentityRecoveryNotice |
| [T056](../reactors/complete-account-recovery-reactor.md#t056) | reaction_or_regular_process_stage | EmailAddress | IdentityRecovery | PrepareAccountRecovery |
| [T057](../reactors/complete-account-recovery-reactor.md#t057) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | EndRecoveryIdentityProtection |
| [T058](../reactors/complete-account-recovery-reactor.md#t058) | reaction_or_regular_process_stage | IdentityRecovery | EmailAddress | CompleteEmailRecoveryChallenge |
| [T097](../reactors/complete-account-recovery-reactor.md#t097) | reaction_or_regular_process_stage | IdentityRecovery | IdentityRecovery | AcceptProtectedIdentityRecovery |
| [A017](../reactors/unassigned-domain-choreography.md#a017) | process_abort_binding | EmailAddress | IdentityRecovery | RejectIdentityRecoveryPreparation |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: baseline source not recorded]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/identity-recovery.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
