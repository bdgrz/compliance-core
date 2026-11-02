# AuditOutcome events

Canonical events: **1**. Complete canonical event and available schema records: [owner event JSON](../data/events/audit-outcome.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| AuditOutcomeRecorded | business | bdgrz.examinations.audit-outcome-recorded | 2 | [Placeholder: schema absent] | Retains an externally supplied conclusion and exact issued-report provenance. |

## Event details

### AuditOutcomeRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:AuditOutcomeRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | AuditOutcomeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.examinations.audit-outcome-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Examinations/Events/AuditOutcomeRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | Retains an externally supplied conclusion and exact issued-report provenance. |
| Meaning source | record summary in src/Compliance.Common/Features/Examinations/Events/AuditOutcomeRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
