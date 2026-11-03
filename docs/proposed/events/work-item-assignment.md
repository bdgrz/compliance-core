# WorkItemAssignment events

Canonical events: **3**. Complete canonical event and available schema records: [owner event JSON](../data/events/work-item-assignment.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WorkAssignmentLegacyHistoryImported | WorkAssignmentLegacyHistoryImported | migration_import | bdgrz.work.assignment-history-imported | 2 | [Placeholder: schema absent] | An existing read-only program ledger prefix was sealed into immutable per-item history before its first native write. |
| WorkItemAssignmentEscalated | WorkItemAssignmentEscalated | business | bdgrz.work.escalated | 2 | [Placeholder: schema absent] | A work item's accountability was escalated, recording its current assignee and that it is escalated, as the next revision of its assignment. |
| WorkItemAssignmentRecorded | WorkItemAssignmentRecorded | business | bdgrz.work.assigned | 2 | [Placeholder: schema absent] | A work item was assigned to a member, or left unassigned, recorded as the next revision of its assignment without escalation. |

## Event details

### WorkAssignmentLegacyHistoryImported

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkAssignmentLegacyHistoryImported |
| Selected source event name | WorkAssignmentLegacyHistoryImported |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkAssignmentLegacyHistoryImported |
| Owner selection | exact verified legacy import onto the per-item owner; fenced old ledger has no new writer |
| Target concrete type | WorkAssignmentLegacyHistoryImported |
| Adopted wire | `{"discriminator":"bdgrz.work.assignment-history-imported","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkAssignmentLegacyHistoryImported.cs at 60029b8f. |
| Schema status | excluded_business_schema_not_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An existing read-only program ledger prefix was sealed into immutable per-item history before its first native write. |
| Selected source meaning | An existing read-only program ledger prefix was sealed into immutable per-item history before its first native write. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WorkAssignmentLegacyHistoryImported.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkItemAssignmentEscalated

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkItemAssignmentEscalated |
| Selected source event name | WorkItemAssignmentEscalated |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkItemAssignmentEscalated |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkItemAssignmentEscalated |
| Adopted wire | `{"discriminator":"bdgrz.work.escalated","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkItemAssignmentEscalated.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A work item's accountability was escalated, recording its current assignee and that it is escalated, as the next revision of its assignment. |
| Selected source meaning | A work item's accountability was escalated, recording its current assignee and that it is escalated, as the next revision of its assignment. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Work/Events/WorkItemAssignmentEscalated.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkItemAssignmentRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkItemAssignmentRecorded |
| Selected source event name | WorkItemAssignmentRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WorkItemAssignmentRecorded |
| Owner selection | one exact canonical authority owner |
| Target concrete type | WorkItemAssignmentRecorded |
| Adopted wire | `{"discriminator":"bdgrz.work.assigned","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkItemAssignmentRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A work item was assigned to a member, or left unassigned, recorded as the next revision of its assignment without escalation. |
| Selected source meaning | A work item was assigned to a member, or left unassigned, recorded as the next revision of its assignment without escalation. |
| Meaning source | authored 2026-10-29 from src/Compliance.Common/Features/Work/Events/WorkItemAssignmentRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
