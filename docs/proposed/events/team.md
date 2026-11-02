# Team events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/team.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| TeamDefined | business | bdgrz.rbac.team.defined | 1 | [Placeholder: schema absent] | A team was defined. The fact is the native acceptance of the logical operation that created it. |
| TeamDeleted | business | bdgrz.rbac.team.deleted | 1 | [Placeholder: schema absent] | A team was deleted. The fact is the native acceptance of the deletion and the source of team cleanup. |

## Event details

### TeamDefined

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TeamDefined |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamDefined |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team.defined","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/TeamDefined.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A team was defined. The fact is the native acceptance of the logical operation that created it. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TeamDefined.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S012](../reactors/tenant-rbac-bootstrap-reactor.md#s012) | emitted semantic fact | Tenant | DefineTeam | Team |

### TeamDeleted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TeamDeleted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TeamDeleted |
| Adopted wire | `{"discriminator":"bdgrz.rbac.team.deleted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/TeamDeleted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A team was deleted. The fact is the native acceptance of the deletion and the source of team cleanup. |
| Meaning source | record summary in src/Compliance.Common/Features/AccessControl/Events/TeamDeleted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S010](../reactors/team-cleanup-reactor.md#s010) | physical root, logical prerequisite | Team | RemoveTeamMember | TeamMember |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
