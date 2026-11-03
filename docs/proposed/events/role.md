# Role events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/role.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| RoleDefined | RoleDefined | business | bdgrz.rbac.role.defined | 1 | [Placeholder: schema absent] | A role was defined. The fact is the native acceptance of the logical operation that created it. |
| RoleDeleted | RoleDeleted | business | bdgrz.rbac.role.deleted | 1 | [Placeholder: schema absent] | A role was deleted. The fact is the native acceptance of the deletion and the source of role cleanup. |
| RoleRenamed | RoleRenamed | business | bdgrz.rbac.role.renamed | 1 | [Placeholder: schema absent] | A role was renamed. The fact is the native acceptance of the rename. |

## Event details

### RoleDefined

| Canonical event detail | Value |
| --- | --- |
| Target event name | RoleDefined |
| Selected source event name | RoleDefined |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RoleDefined |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RoleDefined |
| Adopted wire | `{"discriminator":"bdgrz.rbac.role.defined","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/RoleDefined.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role was defined. The fact is the native acceptance of the logical operation that created it. |
| Selected source meaning | A role was defined. The fact is the native acceptance of the logical operation that created it. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/RoleDefined.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S011](../reactors/set-up-starting-tenant-access-reactor.md#s011) | emitted semantic fact | Tenant | DefineRole | Role |

### RoleDeleted

| Canonical event detail | Value |
| --- | --- |
| Target event name | RoleDeleted |
| Selected source event name | RoleDeleted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RoleDeleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RoleDeleted |
| Adopted wire | `{"discriminator":"bdgrz.rbac.role.deleted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/RoleDeleted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role was deleted. The fact is the native acceptance of the deletion and the source of role cleanup. |
| Selected source meaning | A role was deleted. The fact is the native acceptance of the deletion and the source of role cleanup. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/RoleDeleted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S008](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s008) | physical root, logical prerequisite | Role | RemoveRolePermission | RolePermission |
| [S009](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s009) | physical root, logical prerequisite | Role | RemoveTeamRole | TeamRole |

### RoleRenamed

| Canonical event detail | Value |
| --- | --- |
| Target event name | RoleRenamed |
| Selected source event name | RoleRenamed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RoleRenamed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RoleRenamed |
| Adopted wire | `{"discriminator":"bdgrz.rbac.role.renamed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/RoleRenamed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role was renamed. The fact is the native acceptance of the rename. |
| Selected source meaning | A role was renamed. The fact is the native acceptance of the rename. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/RoleRenamed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
