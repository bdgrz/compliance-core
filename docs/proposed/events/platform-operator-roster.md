# PlatformOperatorRoster events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/platform-operator-roster.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| PlatformOperatorGranted | PlatformOperatorGranted | business | bdgrz.platform.operator.granted | 1 | [Placeholder: schema absent] | Platform operator authority was granted to a user by a named actor with a stated reason. |
| PlatformOperatorRevoked | PlatformOperatorRevoked | business | bdgrz.platform.operator.revoked | 1 | [Placeholder: schema absent] | A user's platform operator authority was revoked by a named actor with a stated reason, ending the access that assignment gave. |
| PlatformOperatorRosterCreated | PlatformOperatorRosterSeeded | business | bdgrz.platform.operator-roster.seeded | 1 | [Placeholder: schema absent] | The platform operator roster was initialised with its starting set of operator users by a named actor. |

## Event details

### PlatformOperatorGranted

| Canonical event detail | Value |
| --- | --- |
| Target event name | PlatformOperatorGranted |
| Selected source event name | PlatformOperatorGranted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PlatformOperatorGranted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PlatformOperatorGranted |
| Adopted wire | `{"discriminator":"bdgrz.platform.operator.granted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/PlatformOperatorGranted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Platform operator authority was granted to a user by a named actor with a stated reason. |
| Selected source meaning | Platform operator authority was granted to a user by a named actor with a stated reason. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/PlatformOperatorGranted.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PlatformOperatorRevoked

| Canonical event detail | Value |
| --- | --- |
| Target event name | PlatformOperatorRevoked |
| Selected source event name | PlatformOperatorRevoked |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PlatformOperatorRevoked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PlatformOperatorRevoked |
| Adopted wire | `{"discriminator":"bdgrz.platform.operator.revoked","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRevoked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A user's platform operator authority was revoked by a named actor with a stated reason, ending the access that assignment gave. |
| Selected source meaning | A user's platform operator authority was revoked by a named actor with a stated reason, ending the access that assignment gave. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRevoked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PlatformOperatorRosterCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | PlatformOperatorRosterCreated |
| Selected source event name | PlatformOperatorRosterSeeded |
| Name decision | Names the completed creation of the roster's initial set of operator users. |
| Canonical semantic key | event:PlatformOperatorRosterSeeded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PlatformOperatorRosterSeeded |
| Adopted wire | `{"discriminator":"bdgrz.platform.operator-roster.seeded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRosterSeeded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The platform operator roster was initialised with its starting set of operator users by a named actor. |
| Selected source meaning | The platform operator roster was initialised with its starting set of operator users by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Tenants/Events/PlatformOperatorRosterSeeded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
