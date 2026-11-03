# TenantAdministratorContinuity

- Classification: `business_governor`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`
- Selected source owner: `TenantManagerGuard`.

## Identity and invariant

**Selected identity:** Tenant singleton manager guard in its existing address. Selected CommandId binds exact contributor/member episode, generation and withdrawal/restoration transition; current complete eligible contributors remain authoritative.

**Invariant:** Removal from effective administrator set must leave another active tenant administrator.

**Additional decision rule:** Administrator admission exactly selects the historical membership episode and its pending restoration, so a withdrawal or deprovisioning cannot drop the set below one active administrator.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [CompleteAdministratorWithdrawal](../commands/tenant-administrator-continuity.md#completeadministratorwithdrawal) | receiver | CommandId |
| [CompleteAdministratorRestoration](../commands/tenant-administrator-continuity.md#completeadministratorrestoration) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantAdministratorRestorationAuthorized](../events/tenant-administrator-continuity.md#tenantadministratorrestorationauthorized) | bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted | 2 |
| [TenantManagerRestorationCompleted](../events/tenant-administrator-continuity.md#tenantmanagerrestorationcompleted) | bdgrz.tenant-manager-guard.tenant-manager-restoration-completed | 2 |
| [TenantManagerRestored](../events/tenant-administrator-continuity.md#tenantmanagerrestored) | bdgrz.rbac.manager_guard.restored | 2 |
| [TenantMemberChangeAuthorized](../events/tenant-administrator-continuity.md#tenantmemberchangeauthorized) | bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted | 2 |
| [TenantAdministratorWithdrawalAuthorized](../events/tenant-administrator-continuity.md#tenantadministratorwithdrawalauthorized) | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted | 2 |
| [TenantManagerWithdrawalCompleted](../events/tenant-administrator-continuity.md#tenantmanagerwithdrawalcompleted) | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberWithdrawal |
| [T047](../reactors/administrator-assignment-withdrawal-reactor.md#t047) | reaction_or_regular_process_stage | TenantAdministratorContinuity | TeamMember | ApplyAdministratorAssignmentWithdrawal |
| [T048](../reactors/apply-accepted-member-status-change-reactor.md#t048) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberStatusChange |
| [T049](../reactors/remove-unneeded-administrator-assignment-reactor.md#t049) | reaction_or_regular_process_stage | TenantAdministratorContinuity | TeamMember | RemoveAdministratorAssignment |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | reaction_or_regular_process_stage | Member\|TeamMember | TenantAdministratorContinuity | CompleteAdministratorWithdrawal |
| [T051](../reactors/administrator-restoration-reactor.md#t051) | reaction_or_regular_process_stage | TenantAdministratorContinuity | Member | ApplyMemberRestoration |
| [T052](../reactors/administrator-restoration-reactor.md#t052) | reaction_or_regular_process_stage | Member\|TeamMember | TenantAdministratorContinuity | CompleteAdministratorRestoration |
| [T098](../reactors/administrator-assignment-restoration-reactor.md#t098) | reaction_or_regular_process_stage | TenantAdministratorContinuity | TeamMember | ApplyAdministratorAssignmentRestoration |

## Product rules

- **MD-04** — The last active tenant administrator cannot be deprovisioned. Decision: [member-deprovisioning.md](../decisions/member-deprovisioning.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant-administrator-continuity.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
