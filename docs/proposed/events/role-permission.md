# RolePermission events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/role-permission.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| RolePermissionAssigned | business | bdgrz.rbac.role-permission.assigned | 1 | [Placeholder: schema absent] | A permission was assigned to a role by a named actor, so holders of the role gain it. |
| RolePermissionRemoved | business | bdgrz.rbac.role-permission.removed | 1 | [Placeholder: schema absent] | A permission was removed from a role by a named actor, ending the assignment episode so holders of the role lose it. |

## Event details

### RolePermissionAssigned

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RolePermissionAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RolePermissionAssigned |
| Adopted wire | `{"discriminator":"bdgrz.rbac.role-permission.assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/RolePermissionAssigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A permission was assigned to a role by a named actor, so holders of the role gain it. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/RolePermissionAssigned.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S015](../reactors/tenant-rbac-bootstrap-reactor.md#s015) | emitted semantic fact | Tenant | AssignRolePermission | RolePermission |

### RolePermissionRemoved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:RolePermissionRemoved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RolePermissionRemoved |
| Adopted wire | `{"discriminator":"bdgrz.rbac.role-permission.removed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/RolePermissionRemoved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A permission was removed from a role by a named actor, ending the assignment episode so holders of the role lose it. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/RolePermissionRemoved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S008](../reactors/role-cleanup-reactor.md#s008) | emitted semantic fact | Role | RemoveRolePermission | RolePermission |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
