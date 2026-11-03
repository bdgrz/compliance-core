# WorkforceObservationResolution events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/workforce-observation-resolution.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WorkforceObservationResolved | WorkforceObservationResolved | business | bdgrz.workforce.observation.resolved | 1 | [Placeholder: schema absent] | A workforce observation was closed as resolved or dismissed with a note by a named actor. |

## Event details

### WorkforceObservationResolved

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkforceObservationResolved |
| Selected source event name | WorkforceObservationResolved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkforceObservationResolved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkforceObservationResolved |
| Adopted wire | `{"discriminator":"bdgrz.workforce.observation.resolved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/WorkforceObservationResolved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A workforce observation was closed as resolved or dismissed with a note by a named actor. |
| Selected source meaning | A workforce observation was closed as resolved or dismissed with a note by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/WorkforceObservationResolved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
