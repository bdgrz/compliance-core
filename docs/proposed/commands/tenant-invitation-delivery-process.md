# TenantInvitationDeliveryProcess commands

Selected command contracts: **0**. Complete selected contracts and companion source evidence: [owner command JSON](../data/commands/tenant-invitation-delivery-process.json).

[Placeholder: no selected command contract is recorded for this owner.]

## Selected command details

[Placeholder: command-level contract details are not recorded.]

## Source contracts

[Placeholder: no companion target-command source records match this owner.]

[Placeholder: owner-wide caller/channel authorization boundary is not consolidated in this catalog.]


## Current source write requests without selected target entries

These request types exist in the current source and have handlers or receiver implementations, but are not included in this owner's selected command contracts above. This section records their implemented request shape without implying that the proposed target has approved a separate owner or business rule.

### SendTenantInvitation

Sends the exact frozen invitation payload to its recipient. The step is the external effect of the delivery process: it records one attempt with a stable identity, sends, and keeps the result on that attempt. A resumed step after an unknown outcome sends the same attempt again, never a new one.

- Source: [src/Compliance.Common/Features/Tenants/Commands/SendTenantInvitation.cs](../../../src/Compliance.Common/Features/Tenants/Commands/SendTenantInvitation.cs)
- Handler: [src/Compliance.Core/Features/Tenants/Handlers/SendTenantInvitationHandler.cs](../../../src/Compliance.Core/Features/Tenants/Handlers/SendTenantInvitationHandler.cs)
- Discriminator: `bdgrz.tenant-invitation.delivery.send@1`
- Request interfaces: ITenantInvitationDeliveryReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<TenantMemberInvited> | true |
| TenantId | Uuid | true |
| InvitationId | Uuid | true |
| Payload | ImmutableContentRef | true |

### RecordTenantInvitationDeliverySent

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Tenants/Commands/RecordTenantInvitationDeliverySent.cs](../../../src/Compliance.Common/Features/Tenants/Commands/RecordTenantInvitationDeliverySent.cs)
- Handler: [src/Compliance.Core/Features/Tenants/Handlers/RecordTenantInvitationDeliverySentHandler.cs](../../../src/Compliance.Core/Features/Tenants/Handlers/RecordTenantInvitationDeliverySentHandler.cs)
- Discriminator: `bdgrz.tenant-invitation.delivery.record-sent@1`
- Request interfaces: ITenantInvitationDeliveryReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<TenantMemberInvited> | true |
| TenantId | Uuid | true |
| InvitationId | Uuid | true |
| AttemptId | Uuid | true |
| SentAt | DateTimeOffset | true |

### RecordTenantInvitationDeliveryFailed

[Placeholder: source summary not recorded]

- Source: [src/Compliance.Common/Features/Tenants/Commands/RecordTenantInvitationDeliveryFailed.cs](../../../src/Compliance.Common/Features/Tenants/Commands/RecordTenantInvitationDeliveryFailed.cs)
- Handler: [src/Compliance.Core/Features/Tenants/Handlers/RecordTenantInvitationDeliveryFailedHandler.cs](../../../src/Compliance.Core/Features/Tenants/Handlers/RecordTenantInvitationDeliveryFailedHandler.cs)
- Discriminator: `bdgrz.tenant-invitation.delivery.record-failed@1`
- Request interfaces: ITenantInvitationDeliveryReceiver
- Response: `ReceiverOutcome`
- HTTP route: none recorded; dispatched by its internal workflow/receiver.

| Field | Type | Required |
| --- | --- | --- |
| CommandId | Uuid | true |
| Source | SourceFactProof<TenantMemberInvited> | true |
| TenantId | Uuid | true |
| InvitationId | Uuid | true |
| AttemptId | Uuid | true |
| FailureCode | string | true |
| FailedAt | DateTimeOffset | true |
