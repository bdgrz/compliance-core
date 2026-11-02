# ManagementAssertion events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/management-assertion.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| ManagementAssertionApproved | business | bdgrz.examinations.management-assertion-approved | 1 | [Placeholder: schema absent] | Records an independent management decision on one exact assertion version. |
| ManagementAssertionRecorded | business | bdgrz.examinations.management-assertion-recorded | 2 | [Placeholder: schema absent] | Creates a versioned assertion and records its exact input bindings and wording. |
| ManagementAssertionRevised | business | bdgrz.examinations.management-assertion-revised | 1 | [Placeholder: schema absent] | Appends a new immutable wording and input binding to a management assertion. |

## Event details

### ManagementAssertionApproved

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ManagementAssertionApproved |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ManagementAssertionApproved |
| Adopted wire | `{"discriminator":"bdgrz.examinations.management-assertion-approved","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionApproved.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Records an independent management decision on one exact assertion version. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionApproved.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ManagementAssertionRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ManagementAssertionRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ManagementAssertionRecorded |
| Adopted wire | `{"discriminator":"bdgrz.examinations.management-assertion-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Creates a versioned assertion and records its exact input bindings and wording. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### ManagementAssertionRevised

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:ManagementAssertionRevised |
| Owner selection | one exact canonical authority owner |
| Target concrete type | ManagementAssertionRevised |
| Adopted wire | `{"discriminator":"bdgrz.examinations.management-assertion-revised","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRevised.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Appends a new immutable wording and input binding to a management assertion. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/ManagementAssertionRevised.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
