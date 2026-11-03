# ExaminationSignOff events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/examination-sign-off.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ExaminationSignedOff | ExaminationSignedOff | business | bdgrz.examinations.examination-signed-off | 2 | [Placeholder: schema absent] | Records management's immutable review of the exact examination inputs and outcomes. |

## Event details

### ExaminationSignedOff

| Canonical event detail | Value |
| --- | --- |
| Target event name | ExaminationSignedOff |
| Selected source event name | ExaminationSignedOff |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ExaminationSignedOff |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ExaminationSignedOff |
| Adopted wire | `{"discriminator":"bdgrz.examinations.examination-signed-off","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ExaminationSignedOff.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records management's immutable review of the exact examination inputs and outcomes. |
| Selected source meaning | Records management's immutable review of the exact examination inputs and outcomes. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/ExaminationSignedOff.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X008](../reactors/unassigned-domain-choreography.md#x008) | emitted semantic fact | ManagementRepresentationLetter | SignOffExamination | ExaminationSignOff |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
