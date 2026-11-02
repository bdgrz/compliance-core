# WorkDigestDispatch events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/work-digest-dispatch.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| WorkDigestDispatchAttemptStarted | technical | bdgrz.work.digest_dispatch.attempt_started | 1 | [Placeholder: schema absent] | null (recorded) |
| WorkDigestDispatchOutcomeRecorded | technical | bdgrz.work.digest_dispatch.outcome_recorded | 1 | [Placeholder: schema absent] | null (recorded) |
| WorkDigestDispatchScheduled | technical | bdgrz.work.digest_dispatch.scheduled | 1 | [Placeholder: schema absent] | null (recorded) |
| WorkDigestUnknownRetryAuthorized | technical | bdgrz.work.digest_dispatch.unknown_retry_authorized | 2 | [Placeholder: schema absent] | A native operator admission bound to its exact unknown attempt and immutable original result. |

## Event details

### WorkDigestDispatchAttemptStarted

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkDigestDispatchAttemptStarted |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchAttemptStarted |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.attempt_started","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchAttemptStarted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestDispatchOutcomeRecorded

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkDigestDispatchOutcomeRecorded |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchOutcomeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.outcome_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchOutcomeRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestDispatchScheduled

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkDigestDispatchScheduled |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchScheduled |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.scheduled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchScheduled.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | null (recorded) |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestUnknownRetryAuthorized

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:WorkDigestUnknownRetryAuthorized |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestUnknownRetryAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.unknown_retry_authorized","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestUnknownRetryAuthorized.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | A native operator admission bound to its exact unknown attempt and immutable original result. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WorkDigestUnknownRetryAuthorized.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
