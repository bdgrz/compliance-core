# WeeklyWorkDigest events

Canonical events: **4**. Complete canonical event and available schema records: [owner event JSON](../data/events/weekly-work-digest.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| WeeklyWorkDigestDeliveryRecorded | WeeklyWorkDigestDeliveryRecorded | technical | bdgrz.work.weekly-digest.delivery-recorded | 2 | [Placeholder: schema absent] | Records the durable outcome of one exact weekly digest delivery attempt. |
| WeeklyWorkDigestDeliveryRequested | WeeklyWorkDigestDeliveryRequested | technical | bdgrz.work.weekly-digest.delivery-requested | 2 | [Placeholder: schema absent] | Records one deterministic email delivery attempt for a scheduled weekly digest. |
| WeeklyWorkDigestDeliverySuppressed | WeeklyWorkDigestDeliverySuppressed | technical | bdgrz.work.weekly-digest.delivery-suppressed | 2 | [Placeholder: schema absent] | Records why a scheduled digest was suppressed before an email attempt began. |
| WeeklyWorkDigestScheduled | WeeklyWorkDigestScheduled | technical | bdgrz.work.weekly-digest.scheduled | 1 | [Placeholder: schema absent] | Schedules one member's stable weekly digest identity for delivery processing. |

## Event details

### WeeklyWorkDigestDeliveryRecorded

| Canonical event detail | Value |
| --- | --- |
| Target event name | WeeklyWorkDigestDeliveryRecorded |
| Selected source event name | WeeklyWorkDigestDeliveryRecorded |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WeeklyWorkDigestDeliveryRecorded |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WeeklyWorkDigestDeliveryRecorded |
| Adopted wire | `{"discriminator":"bdgrz.work.weekly-digest.delivery-recorded","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRecorded.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records the durable outcome of one exact weekly digest delivery attempt. |
| Selected source meaning | Records the durable outcome of one exact weekly digest delivery attempt. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRecorded.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WeeklyWorkDigestDeliveryRequested

| Canonical event detail | Value |
| --- | --- |
| Target event name | WeeklyWorkDigestDeliveryRequested |
| Selected source event name | WeeklyWorkDigestDeliveryRequested |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WeeklyWorkDigestDeliveryRequested |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WeeklyWorkDigestDeliveryRequested |
| Adopted wire | `{"discriminator":"bdgrz.work.weekly-digest.delivery-requested","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRequested.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records one deterministic email delivery attempt for a scheduled weekly digest. |
| Selected source meaning | Records one deterministic email delivery attempt for a scheduled weekly digest. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliveryRequested.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WeeklyWorkDigestDeliverySuppressed

| Canonical event detail | Value |
| --- | --- |
| Target event name | WeeklyWorkDigestDeliverySuppressed |
| Selected source event name | WeeklyWorkDigestDeliverySuppressed |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WeeklyWorkDigestDeliverySuppressed |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WeeklyWorkDigestDeliverySuppressed |
| Adopted wire | `{"discriminator":"bdgrz.work.weekly-digest.delivery-suppressed","version":2}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliverySuppressed.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Records why a scheduled digest was suppressed before an email attempt began. |
| Selected source meaning | Records why a scheduled digest was suppressed before an email attempt began. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestDeliverySuppressed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### WeeklyWorkDigestScheduled

| Canonical event detail | Value |
| --- | --- |
| Target event name | WeeklyWorkDigestScheduled |
| Selected source event name | WeeklyWorkDigestScheduled |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:WeeklyWorkDigestScheduled |
| Owner selection | exact declared technical/storage owner |
| Target concrete type | WeeklyWorkDigestScheduled |
| Adopted wire | `{"discriminator":"bdgrz.work.weekly-digest.scheduled","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestScheduled.cs at 60029b8f. |
| Schema status | retained_current_schema_native_qualification_required |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | Schedules one member's stable weekly digest identity for delivery processing. |
| Selected source meaning | Schedules one member's stable weekly digest identity for delivery processing. |
| Meaning source | record summary in src/Compliance.Common/Features/Work/Events/WeeklyWorkDigestScheduled.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
