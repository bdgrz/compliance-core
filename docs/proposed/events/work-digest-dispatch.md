# WorkDigestDispatch events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/work-digest-dispatch.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WorkDigestDeliveryAttemptStarted | WorkDigestDispatchAttemptStarted | technical | bdgrz.work.digest_dispatch.attempt_started | 1 | [Placeholder: schema absent] | The system began sending one weekly work digest to its intended member. |
| WorkDigestDeliveryOutcomeRecorded | WorkDigestDispatchOutcomeRecorded | technical | bdgrz.work.digest_dispatch.outcome_recorded | 1 | [Placeholder: schema absent] | The system recorded whether one member’s weekly work digest was delivered, failed, or could not be confirmed. |
| WorkDigestDeliveryScheduled | WorkDigestDispatchScheduled | technical | bdgrz.work.digest_dispatch.scheduled | 1 | [Placeholder: schema absent] | The system scheduled one member’s weekly digest for its next delivery time. |
| WorkDigestRetryApprovedAfterUnknownDelivery | WorkDigestUnknownRetryAuthorized | technical | bdgrz.work.digest_dispatch.unknown_retry_authorized | 2 | [Placeholder: schema absent] | A native operator admission bound to its exact unknown attempt and immutable original result. |

## Event details

### WorkDigestDeliveryAttemptStarted

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkDigestDeliveryAttemptStarted |
| Selected source event name | WorkDigestDispatchAttemptStarted |
| Name decision | Names the start of one weekly digest email delivery attempt. |
| Canonical semantic key | event:WorkDigestDispatchAttemptStarted |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchAttemptStarted |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.attempt_started","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchAttemptStarted.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The system began sending one weekly work digest to its intended member. |
| Selected source meaning | [Placeholder: value not recorded] |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestDeliveryOutcomeRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkDigestDeliveryOutcomeRecorded |
| Selected source event name | WorkDigestDispatchOutcomeRecorded |
| Name decision | Names the recorded result of one weekly digest email delivery attempt. |
| Canonical semantic key | event:WorkDigestDispatchOutcomeRecorded |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchOutcomeRecorded |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.outcome_recorded","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchOutcomeRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The system recorded whether one member’s weekly work digest was delivered, failed, or could not be confirmed. |
| Selected source meaning | [Placeholder: value not recorded] |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestDeliveryScheduled

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkDigestDeliveryScheduled |
| Selected source event name | WorkDigestDispatchScheduled |
| Name decision | Names the scheduled delivery of one member’s weekly digest. |
| Canonical semantic key | event:WorkDigestDispatchScheduled |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestDispatchScheduled |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.scheduled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestDispatchScheduled.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | The system scheduled one member’s weekly digest for its next delivery time. |
| Selected source meaning | [Placeholder: value not recorded] |
| Meaning source | [Placeholder: value not recorded] |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WorkDigestRetryApprovedAfterUnknownDelivery

| Canonical event detail | Value |
| --- | --- |
| Target event name | WorkDigestRetryApprovedAfterUnknownDelivery |
| Selected source event name | WorkDigestUnknownRetryAuthorized |
| Name decision | Names the operator's approval to retry a weekly digest when its prior delivery result could not be confirmed. |
| Canonical semantic key | event:WorkDigestUnknownRetryAuthorized |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WorkDigestUnknownRetryAuthorized |
| Adopted wire | `{"discriminator":"bdgrz.work.digest_dispatch.unknown_retry_authorized","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WorkDigestUnknownRetryAuthorized.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | A native operator admission bound to its exact unknown attempt and immutable original result. |
| Selected source meaning | A native operator admission bound to its exact unknown attempt and immutable original result. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WorkDigestUnknownRetryAuthorized.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
