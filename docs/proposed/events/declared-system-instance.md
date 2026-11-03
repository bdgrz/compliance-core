# DeclaredSystemInstance events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/declared-system-instance.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| SystemInstanceRegistered | SystemInstanceRegistered | business | bdgrz.system_instance.registered | 1 | [Placeholder: schema absent] | A system instance of an application was registered with its name, kind and access boundary reference by a named member, as its first revision. |
| SystemInstanceRetired | SystemInstanceRetired | business | bdgrz.system_instance.retired | 1 | [Placeholder: schema absent] | An effective-dated instance retirement. Scope decisions and history remain. |
| SystemInstanceRevised | SystemInstanceRevised | business | bdgrz.applications.system-instance-revised | 1 | [Placeholder: schema absent] | Records the SystemInstanceRevised domain fact for one tenant-owned subject revision. |

## Event details

### SystemInstanceRegistered

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemInstanceRegistered |
| Selected source event name | SystemInstanceRegistered |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemInstanceRegistered |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemInstanceRegistered |
| Adopted wire | `{"discriminator":"bdgrz.system_instance.registered","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/SystemInstanceRegistered.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A system instance of an application was registered with its name, kind and access boundary reference by a named member, as its first revision. |
| Selected source meaning | A system instance of an application was registered with its name, kind and access boundary reference by a named member, as its first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Applications/Events/SystemInstanceRegistered.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemInstanceRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemInstanceRetired |
| Selected source event name | SystemInstanceRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemInstanceRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemInstanceRetired |
| Adopted wire | `{"discriminator":"bdgrz.system_instance.retired","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/SystemInstanceRetired.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An effective-dated instance retirement. Scope decisions and history remain. |
| Selected source meaning | An effective-dated instance retirement. Scope decisions and history remain. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/SystemInstanceRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SystemInstanceRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | SystemInstanceRevised |
| Selected source event name | SystemInstanceRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SystemInstanceRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SystemInstanceRevised |
| Adopted wire | `{"discriminator":"bdgrz.applications.system-instance-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Applications/Events/SystemInstanceRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the SystemInstanceRevised domain fact for one tenant-owned subject revision. |
| Selected source meaning | Records the SystemInstanceRevised domain fact for one tenant-owned subject revision. |
| Meaning source | record summary in src/Compliance.Common/Features/Applications/Events/SystemInstanceRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
