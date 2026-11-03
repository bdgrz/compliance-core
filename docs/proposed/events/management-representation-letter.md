# ManagementRepresentationLetter events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/management-representation-letter.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| ManagementRepresentationLetterSigned | ManagementRepresentationLetterSigned | business | bdgrz.examinations.management-representation-letter-signed | 1 | [Placeholder: schema absent] | A named executive signed the management representation letter, retaining their signed letter artifact; once every required signer has signed the letter is final. |
| RepresentationLetterRecorded | RepresentationLetterRecorded | business | bdgrz.examinations.representation-letter-recorded | 2 | [Placeholder: schema absent] | Records an externally sourced representation letter and its required signers. |

## Event details

### ManagementRepresentationLetterSigned

| Canonical event detail | Value |
| --- | --- |
| Target event name | ManagementRepresentationLetterSigned |
| Selected source event name | ManagementRepresentationLetterSigned |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:ManagementRepresentationLetterSigned |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ManagementRepresentationLetterSigned |
| Adopted wire | `{"discriminator":"bdgrz.examinations.management-representation-letter-signed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ManagementRepresentationLetterSigned.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A named executive signed the management representation letter, retaining their signed letter artifact; once every required signer has signed the letter is final. |
| Selected source meaning | A named executive signed the management representation letter, retaining their signed letter artifact; once every required signer has signed the letter is final. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Examinations/Events/ManagementRepresentationLetterSigned.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [X008](../reactors/unassigned-domain-choreography.md#x008) | physical root, logical prerequisite | ManagementRepresentationLetter | SignOffExamination | ExaminationSignOff |

### RepresentationLetterRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | RepresentationLetterRecorded |
| Selected source event name | RepresentationLetterRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:RepresentationLetterRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | RepresentationLetterRecorded |
| Adopted wire | `{"discriminator":"bdgrz.examinations.representation-letter-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/RepresentationLetterRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records an externally sourced representation letter and its required signers. |
| Selected source meaning | Records an externally sourced representation letter and its required signers. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/RepresentationLetterRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
