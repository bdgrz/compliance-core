# Person events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/person.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| PersonMemberLinkChanged | PersonMembershipCorrelated | business | bdgrz.workforce.person.membership-correlated | 2 | [Placeholder: schema absent] | Native identified person receipt; complete values and mutation proof are immutable content. |
| PersonRecorded | PersonRecorded | business | bdgrz.workforce.person.recorded | 2 | [Placeholder: schema absent] | A person was recorded in the tenant's workforce with a display name, work email and optional personal contact details by a named actor. |
| PersonRevised | PersonRevised | business | bdgrz.workforce.person.revised | 2 | [Placeholder: schema absent] | A recorded person's display name, work email or contact details were revised by a named actor, producing the person's next revision. |

## Event details

### PersonMemberLinkChanged

| Canonical event detail | Value |
| --- | --- |
| Target event name | PersonMemberLinkChanged |
| Selected source event name | PersonMembershipCorrelated |
| Name decision | Names either linking a workforce person to a tenant member or clearing that link; it does not grant access. |
| Canonical semantic key | event:PersonMembershipCorrelated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PersonMembershipCorrelationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.workforce.person.membership-correlated","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Workforce/Events/PersonMembershipCorrelationRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.workforce.person.membership-correlated","version":1}` |
| Target meaning | Native identified person receipt; complete values and mutation proof are immutable content. |
| Selected source meaning | Native identified person receipt; complete values and mutation proof are immutable content. |
| Meaning source | record summary in src/Compliance.Common/Features/Workforce/Events/PersonMembershipCorrelationRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PersonRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | PersonRecorded |
| Selected source event name | PersonRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PersonRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PersonRegistrationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.workforce.person.recorded","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Workforce/Events/PersonRegistrationRecorded.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.workforce.person.recorded","version":1}` |
| Target meaning | A person was recorded in the tenant's workforce with a display name, work email and optional personal contact details by a named actor. |
| Selected source meaning | A person was recorded in the tenant's workforce with a display name, work email and optional personal contact details by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/PersonRegistrationRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### PersonRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | PersonRevised |
| Selected source event name | PersonRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:PersonRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | PersonDetailsRevised |
| Adopted wire | `{"discriminator":"bdgrz.workforce.person.revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Workforce/Events/PersonDetailsRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.workforce.person.revised","version":1}` |
| Target meaning | A recorded person's display name, work email or contact details were revised by a named actor, producing the person's next revision. |
| Selected source meaning | A recorded person's display name, work email or contact details were revised by a named actor, producing the person's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/PersonDetailsRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
