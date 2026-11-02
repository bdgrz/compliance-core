# UserIdentity

- Classification: `business_aggregate`
- Source area: `UserIdentities` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of identity provider + subject. Exact registration/user binding and revoke/replacement episode remain immutable; native operation terms retain that full binding.

**Invariant:** External provider+subject associates to exactly one platform user; revoked identity cannot authenticate and has one exact replacement.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [ContinueWithOidcProvider](../commands/user-identity.md#continuewithoidcprovider) | http | none (no fields; provider and subject come from the authenticated OIDC principal); emits UserIdentityRegistered or UserIdentityAuthenticated, optionally UserIdentityProfileObserved |
| [ContinueWithDeveloperIdentity](../commands/user-identity.md#continuewithdeveloperidentity) | http | none (derived identity provider+subject) |
| [LinkOidcProviderIdentity](../commands/user-identity.md#linkoidcprovideridentity) | http | none (principal-derived) |
| [ProtectRecoveryIdentity](../commands/user-identity.md#protectrecoveryidentity) | receiver | CommandId |
| [SettleRecoveryIdentityProtection](../commands/user-identity.md#settlerecoveryidentityprotection) | receiver | CommandId |
| [RegisterRecoveredIdentity](../commands/user-identity.md#registerrecoveredidentity) | receiver | CommandId |
| [RevokeRecoveredOldIdentity](../commands/user-identity.md#revokerecoveredoldidentity) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RecoveryIdentityProtected](../events/user-identity.md#recoveryidentityprotected) | bdgrz.user-identity.recovery-identity-protected | 1 |
| [RecoveryIdentityProtectionSettled](../events/user-identity.md#recoveryidentityprotectionsettled) | bdgrz.user-identity.recovery-identity-protection-settled | 1 |
| [UserIdentityAuthenticated](../events/user-identity.md#useridentityauthenticated) | bdgrz.user-identity.authenticated | 1 |
| [UserIdentityProfileObserved](../events/user-identity.md#useridentityprofileobserved) | bdgrz.user-identity.profile-observed | 2 |
| [UserIdentityRegistered](../events/user-identity.md#useridentityregistered) | bdgrz.user-identity.registered | 1 |
| [UserIdentityRevoked](../events/user-identity.md#useridentityrevoked) | bdgrz.user-identity.revoked | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T041](../reactors/identity-recovery-acceptance-reactor.md#t041) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | ProtectRecoveryIdentity |
| [T042](../reactors/identity-recovery-acceptance-reactor.md#t042) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | RegisterRecoveredIdentity |
| [T043](../reactors/identity-recovery-acceptance-reactor.md#t043) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | RevokeRecoveredOldIdentity |
| [T057](../reactors/identity-recovery-acceptance-reactor.md#t057) | reaction_or_regular_process_stage | IdentityRecovery | UserIdentity | SettleRecoveryIdentityProtection |
| [S035](../reactors/email-reservation-reactor.md#s035) | business_receiver | UserIdentity | EmailAddress | ReserveEmail |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [user-identity-review-2026-10-29.md](../../product/definitive-aggregate-choreography/user-identity-review-2026-10-29.md)
- Full selected record: [JSON](../data/aggregates/user-identity.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
