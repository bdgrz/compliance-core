# TenantInvitationMailboxClaim events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant-invitation-mailbox-claim.json).

| Event | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- |
| TenantInvitationMailboxClaimed | business | bdgrz.tenant-invitation-mailbox.claimed | 1 | [Placeholder: schema absent] | One invitation holds the tenant mailbox as its single pending invitation. The generation increases with every claim and is state, never part of the stream key. |
| TenantInvitationMailboxReleased | business | bdgrz.tenant-invitation-mailbox.released | 1 | [Placeholder: schema absent] | The invitation that held the mailbox in Generation no longer does, which lets a later invitation claim it. A release names the exact invitation and generation it ends. |

## Event details

### TenantInvitationMailboxClaimed

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TenantInvitationMailboxClaimed |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantInvitationMailboxClaimed |
| Adopted wire | `{"discriminator":"bdgrz.tenant-invitation-mailbox.claimed","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantInvitationMailboxClaimed.cs at 60029b8f. |
| Schema status | current_native_schema_missing_from_selected295 |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | One invitation holds the tenant mailbox as its single pending invitation. The generation increases with every claim and is state, never part of the stream key. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantInvitationMailboxClaimed.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]

### TenantInvitationMailboxReleased

| Canonical event detail | Value |
| --- | --- |
| Canonical semantic key | event:TenantInvitationMailboxReleased |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantInvitationMailboxReleased |
| Adopted wire | `{"discriminator":"bdgrz.tenant-invitation-mailbox.released","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantInvitationMailboxReleased.cs at 60029b8f. |
| Schema status | current_native_schema_missing_from_selected295 |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Meaning | The invitation that held the mailbox in Generation no longer does, which lets a later invitation claim it. A release names the exact invitation and generation it ends. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantInvitationMailboxReleased.cs |

#### Schema

[Placeholder: no selected payload schema matches this canonical event.]

[Placeholder: selected top-level schema fields are not recorded.]

[Placeholder: no nested content schemas are recorded.]

[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

[Placeholder: no selected choreography row names this event as root, prerequisite, or emitted fact.]




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
