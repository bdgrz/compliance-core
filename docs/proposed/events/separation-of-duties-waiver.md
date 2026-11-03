# SeparationOfDutiesWaiver events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/separation-of-duties-waiver.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| SeparationOfDutiesWaiverApproved | SeparationOfDutiesWaiverApproved | business | bdgrz.sod-waiver.approved | 1 | [Placeholder: schema absent] | A separation-of-duties waiver was approved by an Org Admin other than its beneficiary within its approval window, allowing the beneficiary to act in the waived scope. |
| SeparationOfDutiesWaiverRecorded | SeparationOfDutiesWaiverRecorded | business | bdgrz.sod-waiver.recorded | 1 | [Placeholder: schema absent] | A separation-of-duties waiver was requested for a scoped record, naming its beneficiary, requester, rationale and expiry, and awaits approval by a different Org Admin. |

## Event details

### SeparationOfDutiesWaiverApproved

| Canonical event detail | Value |
| --- | --- |
| Target event name | SeparationOfDutiesWaiverApproved |
| Selected source event name | SeparationOfDutiesWaiverApproved |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SeparationOfDutiesWaiverApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SeparationOfDutiesWaiverApproved |
| Adopted wire | `{"discriminator":"bdgrz.sod-waiver.approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A separation-of-duties waiver was approved by an Org Admin other than its beneficiary within its approval window, allowing the beneficiary to act in the waived scope. |
| Selected source meaning | A separation-of-duties waiver was approved by an Org Admin other than its beneficiary within its approval window, allowing the beneficiary to act in the waived scope. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### SeparationOfDutiesWaiverRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | SeparationOfDutiesWaiverRecorded |
| Selected source event name | SeparationOfDutiesWaiverRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:SeparationOfDutiesWaiverRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | SeparationOfDutiesWaiverRecorded |
| Adopted wire | `{"discriminator":"bdgrz.sod-waiver.recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A separation-of-duties waiver was requested for a scoped record, naming its beneficiary, requester, rationale and expiry, and awaits approval by a different Org Admin. |
| Selected source meaning | A separation-of-duties waiver was requested for a scoped record, naming its beneficiary, requester, rationale and expiry, and awaits approval by a different Org Admin. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/AccessControl/Events/SeparationOfDutiesWaiverRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
