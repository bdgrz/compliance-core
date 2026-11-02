# AuditObservation events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/audit-observation.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditObservationCorrected | business | bdgrz.examinations.audit-observation-corrected | 1 | [Placeholder: schema absent] | Retains a received auditor correction while preserving its predecessor in source history. |
| AuditObservationFindingLinked | business | bdgrz.examinations.audit-observation-finding-linked | 2 | [Placeholder: schema absent] | Links an exact finding source revision without taking ownership of its remediation. |
| AuditObservationManagementResponseRecorded | business | bdgrz.examinations.audit-observation-management-response-recorded | 1 | [Placeholder: schema absent] | Retains management's response separately from the externally authored wording. |
| AuditObservationRecorded | business | bdgrz.examinations.audit-observation-recorded | 2 | [Placeholder: schema absent] | Retains received auditor wording and its exact source provenance. |

## Event details

### AuditObservationCorrected

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditObservationCorrected |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditObservationCorrected |
| Adopted wire | `{"discriminator":"bdgrz.examinations.audit-observation-corrected","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/AuditObservationCorrected.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Retains a received auditor correction while preserving its predecessor in source history. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/AuditObservationCorrected.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditObservationFindingLinked

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditObservationFindingLinked |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditObservationFindingLinked |
| Adopted wire | `{"discriminator":"bdgrz.examinations.audit-observation-finding-linked","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/AuditObservationFindingLinked.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Links an exact finding source revision without taking ownership of its remediation. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/AuditObservationFindingLinked.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditObservationManagementResponseRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditObservationManagementResponseRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditObservationManagementResponseRecorded |
| Adopted wire | `{"discriminator":"bdgrz.examinations.audit-observation-management-response-recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/AuditObservationManagementResponseRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Retains management's response separately from the externally authored wording. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/AuditObservationManagementResponseRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### AuditObservationRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditObservationRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditObservationRecorded |
| Adopted wire | `{"discriminator":"bdgrz.examinations.audit-observation-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/AuditObservationRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Retains received auditor wording and its exact source provenance. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/AuditObservationRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
