# TeamRole

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`

## Identity and invariant

**Selected identity:** Existing UUIDv5 of tenant + team + role. Selected CommandId binds the exact assignment episode and removal intent.

**Invariant:** One tenant/team/role relationship has a single active assignment.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]

## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignTeamRole](../commands/team-role.md#assignteamrole) | command | CommandId |
| [RemoveTeamRole](../commands/team-role.md#removeteamrole) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TeamRoleAssigned](../events/team-role.md#teamroleassigned) | bdgrz.rbac.team-role.assigned | 1 |
| [TeamRoleRemoved](../events/team-role.md#teamroleremoved) | bdgrz.rbac.team-role.removed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S009](../reactors/role-cleanup-reactor.md#s009) | business_receiver | Role | TeamRole | RemoveTeamRole |
| [S014](../reactors/tenant-rbac-bootstrap-reactor.md#s014) | business_receiver | Tenant | TeamRole | AssignTeamRole |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/team-role.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
