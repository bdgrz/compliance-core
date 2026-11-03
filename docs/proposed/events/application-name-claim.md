# ApplicationNameClaim events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/application-name-claim.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ApplicationNameClaimed | ApplicationNameClaimed | business | null (recorded) | null (recorded) | [Placeholder: schema absent] | A normalized application name or alias was claimed for one declared application in the tenant; no other active application may hold it. |
| ApplicationNameReleased | ApplicationNameReleased | business | null (recorded) | null (recorded) | [Placeholder: schema absent] | A previously claimed application name or alias was released because the holder was retired, renamed, or merged into another application. |

## Event details

### ApplicationNameClaimed

| Canonical event detail | Value |
| --- | --- |
| Target event name | ApplicationNameClaimed |
| Selected source event name | ApplicationNameClaimed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ApplicationNameClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationNameClaimed |
| Adopted wire | null (recorded) |
| Wire qualification | Planned; no declared record yet. Discriminator to follow the inventory name-claim convention when implemented. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_ApplicationNameClaim |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A normalized application name or alias was claimed for one declared application in the tenant; no other active application may hold it. |
| Selected source meaning | A normalized application name or alias was claimed for one declared application in the tenant; no other active application may hold it. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X000](../reactors/application-name-change-reactor.md#x000) | emitted semantic fact | DeclaredApplication | ClaimApplicationName | ApplicationNameClaim |
| [X001](../reactors/application-name-change-reactor.md#x001) | physical root, logical prerequisite | ApplicationNameClaim | ApplyApplicationNameChange | DeclaredApplication |

### ApplicationNameReleased

| Canonical event detail | Value |
| --- | --- |
| Target event name | ApplicationNameReleased |
| Selected source event name | ApplicationNameReleased |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ApplicationNameReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ApplicationNameReleased |
| Adopted wire | null (recorded) |
| Wire qualification | Planned; no declared record yet. Discriminator to follow the inventory name-claim convention when implemented. |
| Schema status | planned_schema |
| Source status | planned_no_source_type |
| Adopted decision IDs | OWNER_ApplicationNameClaim |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A previously claimed application name or alias was released because the holder was retired, renamed, or merged into another application. |
| Selected source meaning | A previously claimed application name or alias was released because the holder was retired, renamed, or merged into another application. |
| Meaning source | authored 2026-10-29 |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X002](../reactors/application-name-change-reactor.md#x002) | emitted semantic fact | DeclaredApplication | ReleaseApplicationName | ApplicationNameClaim |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
