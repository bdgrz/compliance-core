# WeeklyWorkDigest commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/weekly-work-digest.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### ScheduleWeeklyWorkDigest

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Work/Commands/ScheduleWeeklyWorkDigest.cs](../../../src/Compliance.Common/Features/Work/Commands/ScheduleWeeklyWorkDigest.cs)
- Handler: [src/Compliance.Core/Features/Work/Handlers/ScheduleWeeklyWorkDigestHandler.cs](../../../src/Compliance.Core/Features/Work/Handlers/ScheduleWeeklyWorkDigestHandler.cs)
- Discriminator: `bdgrz.work.schedule-weekly-work-digest@2`
- Request interfaces: IRequest
- Response: `WorkDigestSchedulePageView`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| WeekStarting | DateOnly | true |
| ScheduleKey | string | true |
| Limit | int | false |
| Cursor | string? | false |

### DeliverWeeklyWorkDigest

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Work/Commands/DeliverWeeklyWorkDigest.cs](../../../src/Compliance.Common/Features/Work/Commands/DeliverWeeklyWorkDigest.cs)
- Handler: [src/Compliance.Core/Features/Work/Handlers/DeliverWeeklyWorkDigestHandler.cs](../../../src/Compliance.Core/Features/Work/Handlers/DeliverWeeklyWorkDigestHandler.cs)
- Discriminator: `bdgrz.work.deliver-weekly-work-digest@3`
- Request interfaces: IRequest, IIdentifiedRequest
- Response: `Unit`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| MemberId | Uuid | true |
| DigestId | Uuid | true |
| ScheduleEventId | Uuid | true |
| ExpectedRevision | long | true |
| AttemptNumber | int | true |
| DeliveryKey | string | true |

### RecordWorkDigestDelivery

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Work/Commands/RecordWorkDigestDelivery.cs](../../../src/Compliance.Common/Features/Work/Commands/RecordWorkDigestDelivery.cs)
- Handler: [src/Compliance.Core/Features/Work/Handlers/RecordWorkDigestDeliveryHandler.cs](../../../src/Compliance.Core/Features/Work/Handlers/RecordWorkDigestDeliveryHandler.cs)
- Discriminator: `bdgrz.work.record-work-digest-delivery@3`
- Request interfaces: IRequest, IIdentifiedRequest
- Response: `Unit`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| DigestId | Uuid | true |
| DeliveryRequestEventId | Uuid | true |
| ExpectedRevision | long | true |
| AttemptNumber | int | true |
| DeliveryKey | string | true |
| Outcome | string | true |
| AttemptedAt | DateTimeOffset | true |
| FailureCode | string? | true |
