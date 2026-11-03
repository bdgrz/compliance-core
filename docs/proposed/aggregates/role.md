# Role

- Classification: `business_aggregate`
- Source area: `AccessControl` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant + RoleId; built-in identities remain fixed. RoleId identifies immutable creation; selected mutations use CommandId with current authorized role scope.

**Invariant:** One role has one active/deleted lifecycle, normalized display name and built-in protection.

**Additional decision rule:** The built-in role catalog is fixed to Org Admin, Compliance Lead, Contributor, Viewer, Advisor and Attest, and custom roles are not supported; a built-in role cannot be deleted.

**Must preserve:** [Placeholder: preservation rule not recorded.]

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [DefineRole](../commands/role.md#definerole) | http | RoleId |
| [DeleteRole](../commands/role.md#deleterole) | command | CommandId |
| [RenameRole](../commands/role.md#renamerole) | command | CommandId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [RoleDefined](../events/role.md#roledefined) | bdgrz.rbac.role.defined | 1 |
| [RoleDeleted](../events/role.md#roledeleted) | bdgrz.rbac.role.deleted | 1 |
| [RoleRenamed](../events/role.md#rolerenamed) | bdgrz.rbac.role.renamed | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [S008](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s008) | business_receiver | Role | RolePermission | RemoveRolePermission |
| [S009](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s009) | business_receiver | Role | TeamRole | RemoveTeamRole |
| [S011](../reactors/set-up-starting-tenant-access-reactor.md#s011) | business_receiver | Tenant | Role | DefineRole |

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/role.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
