# WorkDigestDispatch commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/work-digest-dispatch.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]

## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### AuthorizeWorkDigestUnknownRetry

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Work/Commands/AuthorizeWorkDigestUnknownRetry.cs](../../../src/Compliance.Common/Features/Work/Commands/AuthorizeWorkDigestUnknownRetry.cs)
- Handler: [src/Compliance.Core/Features/Work/Handlers/AuthorizeWorkDigestUnknownRetryHandler.cs](../../../src/Compliance.Core/Features/Work/Handlers/AuthorizeWorkDigestUnknownRetryHandler.cs)
- Discriminator: `bdgrz.work.digest.dispatch.unknown_retry.authorize@2`
- Request interfaces: IRequest, ICallable, IPlatformOperatorRequest, IIdentifiedRequest
- Response: `WorkDigestDispatchStatusView`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| TenantId | Uuid | true |
| MemberId | Uuid | true |
| WeekOf | DateOnly | true |
| EvidenceReference | string | true |
| Rationale | string | true |
| ExpectedRevision | long | true |
