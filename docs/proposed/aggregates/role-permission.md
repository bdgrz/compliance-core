# RolePermission

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Existing UUIDv5 of tenant + role + normalized permission. Selected CommandId and assignment episode bind the exact assignment/removal intent.

**Invariant:** One normalized role-permission tuple has a single active relationship at a time.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AssignRolePermission](../commands/role-permission.md#assignrolepermission) | command | CommandId |
| [RemoveRolePermission](../commands/role-permission.md#removerolepermission) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RolePermissionAssigned](../events/role-permission.md#rolepermissionassigned) | bdgrz.rbac.role-permission.assigned | 1 |
| [RolePermissionRemoved](../events/role-permission.md#rolepermissionremoved) | bdgrz.rbac.role-permission.removed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S008](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s008) | business_receiver | Role | RolePermission | RemoveRolePermission |
| [S015](../reactors/set-up-starting-tenant-access-reactor.md#s015) | business_receiver | Tenant | RolePermission | AssignRolePermission |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/role-permission.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
