# ClientService events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/client-service.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ClientServiceCreated | ClientServiceCreated | business | bdgrz.client-service.created | 2 | [Placeholder: schema absent] | A client service was created under its owning program, recorded as the service's first revision. |
| ClientServiceRetired | ClientServiceRetired | business | bdgrz.client-service.retired | 2 | [Placeholder: schema absent] | A client service of a program was retired with a retirement record, ending the service while keeping its last content. |
| ClientServiceRevised | ClientServiceRevised | business | bdgrz.client-service.revised | 2 | [Placeholder: schema absent] | The content of a client service under a program was revised, recorded as the service's next revision. |

## Event details

### ClientServiceCreated

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientServiceCreated |
| Selected source event name | ClientServiceCreated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientServiceCreated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientServiceCreated |
| Adopted wire | `{"discriminator":"bdgrz.client-service.created","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Programs/Events/ClientServiceCreated.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.client-service.created","version":1}` |
| Target meaning | A client service was created under its owning program, recorded as the service's first revision. |
| Selected source meaning | A client service was created under its owning program, recorded as the service's first revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ClientServiceCreated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ClientServiceRetired

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientServiceRetired |
| Selected source event name | ClientServiceRetired |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientServiceRetired |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientServiceRetired |
| Adopted wire | `{"discriminator":"bdgrz.client-service.retired","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Programs/Events/ClientServiceRetired.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.client-service.retired","version":1}` |
| Target meaning | A client service of a program was retired with a retirement record, ending the service while keeping its last content. |
| Selected source meaning | A client service of a program was retired with a retirement record, ending the service while keeping its last content. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ClientServiceRetired.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ClientServiceRevised

| Canonical event detail | Value |
| --- | --- |
| Target event name | ClientServiceRevised |
| Selected source event name | ClientServiceRevised |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ClientServiceRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ClientServiceRevised |
| Adopted wire | `{"discriminator":"bdgrz.client-service.revised","version":2}` |
| Wire qualification | Adopted wire is the occupied discriminator declared in src/Compliance.Common/Features/Programs/Events/ClientServiceRevised.cs at 60029b8f (2026-10-29 reconciliation). The earlier selected wire is retained as supersededSelectedWire for provenance only. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | `{"discriminator":"bdgrz.client-service.revised","version":1}` |
| Target meaning | The content of a client service under a program was revised, recorded as the service's next revision. |
| Selected source meaning | The content of a client service under a program was revised, recorded as the service's next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Programs/Events/ClientServiceRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
