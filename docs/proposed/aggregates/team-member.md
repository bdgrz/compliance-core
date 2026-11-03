# TeamMember

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of tenant + team + member. Each assignment episode remains exact; selected native operation/receiver identity is bound to that member/team episode.

**Invariant:** One tenant/team/member relationship is active only for one current membership episode.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignTeamMember](../commands/team-member.md#assignteammember) | http | CommandId |
| [RemoveTeamMember](../commands/team-member.md#removeteammember) | http | CommandId |
| [ApplyAdministratorAssignmentRestoration](../commands/team-member.md#applyadministratorassignmentrestoration) | receiver | CommandId |
| [ApplyAdministratorAssignmentWithdrawal](../commands/team-member.md#applyadministratorassignmentwithdrawal) | receiver | CommandId |
| [RemoveAdministratorAssignment](../commands/team-member.md#removeadministratorassignment) | receiver | CommandId |
| [RemoveRetiredMemberAccessAndAssignments](../commands/team-member.md#removeretiredmemberaccessandassignments) | reactor | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TeamMemberAssigned](../events/team-member.md#teammemberassigned) | bdgrz.rbac.team-member.assigned | 1 |
| [TeamMemberRemoved](../events/team-member.md#teammemberremoved) | bdgrz.rbac.team-member.removed | 2 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T047](../reactors/administrator-assignment-withdrawal-reactor.md#t047) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | ApplyAdministratorAssignmentWithdrawal |
| [T049](../reactors/administrator-neutral-assignment-mutation-reactor.md#t049) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | RemoveAdministratorAssignment |
| [T098](../reactors/administrator-assignment-restoration-reactor.md#t098) | reaction_or_regular_process_stage | TenantManagerGuard | TeamMember | ApplyAdministratorAssignmentRestoration |
| [S004](../reactors/member-retirement-reactor.md#s004) | business_receiver | Member | TeamMember | RemoveRetiredMemberAccessAndAssignments |
| [S007](../reactors/retired-team-assignment-reactor.md#s007) | business_receiver | TeamMember | TeamMember | RemoveRetiredMemberAccessAndAssignments |
| [S010](../reactors/team-cleanup-reactor.md#s010) | business_receiver | Team | TeamMember | RemoveTeamMember |
| [S016](../reactors/tenant-rbac-bootstrap-reactor.md#s016) | business_receiver | Tenant | TeamMember | AssignTeamMember |
| [S027](../reactors/tenant-invitation-reactor.md#s027) | business_receiver | TenantInvitation | TeamMember | AssignTeamMember |
| [S034](../reactors/tenant-self-service-activation-reactor.md#s034) | business_receiver | TeamMember | Tenant | ActivateTenant |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/team-member.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
