# TenantInvitationDeliveryProcess

- Root source events: TenantMemberInvited
- Ordered stages: true

| Stage | Receivers | Rule |
| --- | --- | --- |
| send | SendTenantInvitation | One immutable payload and one attempt identity; a resumed unknown result sends the same attempt. |
| record | RecordTenantInvitationDeliverySent, RecordTenantInvitationDeliveryFailed | Exactly one outcome receiver, selected by the result the attempt stored. |

Abort commands: None listed

Complete selected process definition: [JSON](../data/processes/tenant-invitation-delivery-process.json).

[Placeholder: timeout, scheduling, and operator recovery details not present in the selected process definition.]
