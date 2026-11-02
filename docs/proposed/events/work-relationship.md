# WorkRelationship events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/work-relationship.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| WorkRelationshipRecorded | business | bdgrz.workforce.work-relationship.recorded | 1 | [Placeholder: schema absent] | A person's work relationship with the organisation was recorded under a source worker identifier with its terms by a named actor. |
| WorkRelationshipRevised | business | bdgrz.workforce.work-relationship.revised | 1 | [Placeholder: schema absent] | A recorded work relationship's terms were revised by a named actor, producing its next revision. |

## Event details

### WorkRelationshipRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkRelationshipRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkRelationshipRecorded |
| Adopted wire | `{"discriminator":"bdgrz.workforce.work-relationship.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A person's work relationship with the organisation was recorded under a source worker identifier with its terms by a named actor. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkRelationshipRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkRelationshipRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkRelationshipRevised |
| Adopted wire | `{"discriminator":"bdgrz.workforce.work-relationship.revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A recorded work relationship's terms were revised by a named actor, producing its next revision. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Workforce/Events/WorkRelationshipRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
