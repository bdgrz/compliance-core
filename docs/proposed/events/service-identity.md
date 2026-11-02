# ServiceIdentity events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/service-identity.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ServiceIdentityRecorded | business | bdgrz.workforce.service-identity.recorded | 1 | [Placeholder: schema absent] | A service identity was recorded in the tenant's workforce with its terms by a named actor. |
| ServiceIdentityRevised | business | bdgrz.workforce.service-identity.revised | 1 | [Placeholder: schema absent] | A recorded service identity's terms were revised by a named actor, producing its next revision. |

## Event details

### ServiceIdentityRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceIdentityRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceIdentityRecorded |
| Adopted wire | `{"discriminator":"bdgrz.workforce.service-identity.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A service identity was recorded in the tenant's workforce with its terms by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ServiceIdentityRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ServiceIdentityRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ServiceIdentityRevised |
| Adopted wire | `{"discriminator":"bdgrz.workforce.service-identity.revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A recorded service identity's terms were revised by a named actor, producing its next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/ServiceIdentityRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
