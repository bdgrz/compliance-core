# TeamRole events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/team-role.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TeamRoleAssigned | TeamRoleAssigned | business | bdgrz.rbac.team-role.assigned | 1 | [Placeholder: schema absent] | A role was assigned to a team by a named actor, so members of the team gain the role's permissions. |
| TeamRoleRemoved | TeamRoleRemoved | business | bdgrz.rbac.team-role.removed | 1 | [Placeholder: schema absent] | A role was removed from a team by a named actor, ending the assignment episode so team members lose the role's permissions. |

## Event details

### TeamRoleAssigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | TeamRoleAssigned |
| Selected source event name | TeamRoleAssigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TeamRoleAssigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamRoleAssigned |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team-role.assigned","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/TeamRoleAssigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role was assigned to a team by a named actor, so members of the team gain the role's permissions. |
| Selected source meaning | A role was assigned to a team by a named actor, so members of the team gain the role's permissions. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/TeamRoleAssigned.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S014](../reactors/set-up-starting-tenant-access-reactor.md#s014) | emitted semantic fact | Tenant | AssignTeamRole | TeamRole |

### TeamRoleRemoved

| Canonical event detail | Value |
| --- | --- |
| Target event name | TeamRoleRemoved |
| Selected source event name | TeamRoleRemoved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TeamRoleRemoved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamRoleRemoved |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team-role.removed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/TeamRoleRemoved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A role was removed from a team by a named actor, ending the assignment episode so team members lose the role's permissions. |
| Selected source meaning | A role was removed from a team by a named actor, ending the assignment episode so team members lose the role's permissions. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/TeamRoleRemoved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S009](../reactors/remove-role-assignments-after-role-deletion-reactor.md#s009) | emitted semantic fact | Role | RemoveTeamRole | TeamRole |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
