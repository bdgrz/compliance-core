# TenantManagerGuard

- Classification: `business_governor`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

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
| [CompleteAdministratorWithdrawal](../commands/tenant-manager-guard.md#completeadministratorwithdrawal) | receiver | CommandId |
| [CompleteAdministratorRestoration](../commands/tenant-manager-guard.md#completeadministratorrestoration) | receiver | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantManagerRestorationAccepted](../events/tenant-manager-guard.md#tenantmanagerrestorationaccepted) | bdgrz.tenant-manager-guard.tenant-manager-restoration-accepted | 2 |
| [TenantManagerRestorationCompleted](../events/tenant-manager-guard.md#tenantmanagerrestorationcompleted) | bdgrz.tenant-manager-guard.tenant-manager-restoration-completed | 2 |
| [TenantManagerRestored](../events/tenant-manager-guard.md#tenantmanagerrestored) | bdgrz.rbac.manager_guard.restored | 2 |
| [TenantManagerMemberChangeAccepted](../events/tenant-manager-guard.md#tenantmanagermemberchangeaccepted) | bdgrz.tenant-manager-guard.tenant-manager-source-mutation-admitted | 2 |
| [TenantManagerWithdrawalAccepted](../events/tenant-manager-guard.md#tenantmanagerwithdrawalaccepted) | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-accepted | 2 |
| [TenantManagerWithdrawalCompleted](../events/tenant-manager-guard.md#tenantmanagerwithdrawalcompleted) | bdgrz.tenant-manager-guard.tenant-manager-withdrawal-completed | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T046](../reactors/administrator-withdrawal-reactor.md#t046) | reaction_or_regular_process_stage | TenantManagerGuard | Member | ApplyMemberWithdrawal |
| [T047](../reactors/administrator-assignment-withdrawal-reactor.md#t047) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | ApplyAdministratorAssignmentWithdrawal |
| [T048](../reactors/administrator-neutral-member-mutation-reactor.md#t048) | reaction_or_regular_process_stage | TenantManagerGuard | Member | ApplyMemberStatusChange |
| [T049](../reactors/administrator-neutral-assignment-mutation-reactor.md#t049) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | RemoveAdministratorAssignment |
| [T050](../reactors/administrator-withdrawal-reactor.md#t050) | reaction_or_regular_process_stage | Member\|TeamMember | TenantManagerGuard | CompleteAdministratorWithdrawal |
| [T051](../reactors/administrator-restoration-reactor.md#t051) | reaction_or_regular_process_stage | TenantManagerGuard | Member | ApplyMemberRestoration |
| [T052](../reactors/administrator-restoration-reactor.md#t052) | reaction_or_regular_process_stage | Member\|TeamMember | TenantManagerGuard | CompleteAdministratorRestoration |
| [T098](../reactors/administrator-assignment-restoration-reactor.md#t098) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | ApplyAdministratorAssignmentRestoration |

## Product rules

- **MD-04** — The last active tenant administrator cannot be deprovisioned. Source: [member-deprovisioning.md](../decisions/member-deprovisioning.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: tenant-manager-guard-review-2026-10-29.md
- Full selected record: [JSON](../data/aggregates/tenant-manager-guard.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
