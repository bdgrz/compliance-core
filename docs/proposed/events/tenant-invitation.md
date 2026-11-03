# TenantInvitation events

Canonical events: **2**. Complete canonical event and available schema records: [owner event JSON](../data/events/tenant-invitation.json).

| Target event | Selected source name | Kind | Adopted discriminator | Version | Schema fields | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| TenantInvitationAccepted | TenantInvitationAccepted | business | bdgrz.tenant-invitation.accepted | 1 | 14 | An invitation was accepted by an authenticated user who proved the invited mailbox. The fact is the native acceptance of the invitation's acceptance operation and freezes the exact terms the reaction provisions. GuestProgramIdsManifest references the complete ordered guest program scope. |
| TenantMemberInvited | TenantMemberInvited | business | bdgrz.tenant-member.invited | 1 | 17 | An invitation was created. The fact is the native acceptance of the invitation's logical id on the invitation stream and freezes the exact recipient, purpose, scopes and expiry. The bearer token never enters the fact, only its hash and the key that derives it. MailboxGeneration is the generation of the mailbox claim the invitation was published under; only that generation may release it. GuestProgramIdsManifest references the complete ordered program scope. |

## Event details

### TenantInvitationAccepted

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantInvitationAccepted |
| Selected source event name | TenantInvitationAccepted |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantInvitationAccepted |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantInvitationAccepted |
| Adopted wire | `{"discriminator":"bdgrz.tenant-invitation.accepted","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantInvitationAccepted.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An invitation was accepted by an authenticated user who proved the invited mailbox. The fact is the native acceptance of the invitation's acceptance operation and freezes the exact terms the reaction provisions. GuestProgramIdsManifest references the complete ordered guest program scope. |
| Selected source meaning | An invitation was accepted by an authenticated user who proved the invited mailbox. The fact is the native acceptance of the invitation's acceptance operation and freezes the exact terms the reaction provisions. GuestProgramIdsManifest references the complete ordered guest program scope. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantInvitationAccepted.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantInvitationAccepted |
| Schema discriminator | bdgrz.tenant-invitation.accepted |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantInvitation |
| Schema existingContract | src/Compliance.Common/Features/Tenants/TenantInvitationAccepted.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<Uuid>? to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| UserId | Uuid | true |
| EmailAddress | string | true |
| Affiliation | string | true |
| Administrator | bool | true |
| BuiltInRole | string? | false |
| GuestProgramIdsManifest | ImmutableContentRef | false |
| GuestExpiresOn | DateOnly? | false |
| AcceptedAt | DateTimeOffset | false |
| InvitedBy | Uuid | false |
| InvitedByDisplay | string? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: TenantInvitationAccepted.GuestProgramIdsManifest.v1

| Property | Value |
| --- | --- |
| field | GuestProgramIdsManifest |
| schema | TenantInvitationAccepted.GuestProgramIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid>? |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [S026](../reactors/tenant-invitation-reactor.md#s026) | physical root, logical prerequisite | TenantInvitation | RegisterMember | Member |
| [S027](../reactors/tenant-invitation-reactor.md#s027) | physical root, logical prerequisite | TenantInvitation | AssignTeamMember | TeamMember |
| [S028](../reactors/tenant-invitation-reactor.md#s028) | physical root, logical prerequisite | TenantInvitation | ProvisionGuestProgramAccess | AccessGrant |
| [S029](../reactors/tenant-invitation-reactor.md#s029) | physical root, logical prerequisite | TenantInvitation | ActivateTenant | Tenant |

### TenantMemberInvited

| Canonical event detail | Value |
| --- | --- |
| Target event name | TenantMemberInvited |
| Selected source event name | TenantMemberInvited |
| Name decision | The selected source name is retained. |
| Canonical semantic key | event:TenantMemberInvited |
| Owner selection | one exact canonical authority owner |
| Target concrete type | TenantMemberInvited |
| Adopted wire | `{"discriminator":"bdgrz.tenant-member.invited","version":1}` |
| Wire qualification | Wire matches the declared record in src/Compliance.Common/Features/Tenants/Events/TenantMemberInvited.cs at 60029b8f. |
| Schema status | selected_complete_schema |
| Source status | declared |
| Adopted decision IDs | None listed |
| Superseded selected wire | [Placeholder: value not recorded] |
| Target meaning | An invitation was created. The fact is the native acceptance of the invitation's logical id on the invitation stream and freezes the exact recipient, purpose, scopes and expiry. The bearer token never enters the fact, only its hash and the key that derives it. MailboxGeneration is the generation of the mailbox claim the invitation was published under; only that generation may release it. GuestProgramIdsManifest references the complete ordered program scope. |
| Selected source meaning | An invitation was created. The fact is the native acceptance of the invitation's logical id on the invitation stream and freezes the exact recipient, purpose, scopes and expiry. The bearer token never enters the fact, only its hash and the key that derives it. MailboxGeneration is the generation of the mailbox claim the invitation was published under; only that generation may release it. GuestProgramIdsManifest references the complete ordered program scope. |
| Meaning source | record summary in src/Compliance.Common/Features/Tenants/Events/TenantMemberInvited.cs |

#### Selected schema and implementation evidence

| Property | Value |
| --- | --- |
| Schema event | TenantMemberInvited |
| Schema discriminator | bdgrz.tenant-member.invited |
| Schema discriminatorVersion | 1 |
| Schema owner | TenantInvitation |
| Schema existingContract | src/Compliance.Common/Features/Tenants/TenantMemberInvited.cs |
| Schema status | selected target schema; implementation pending |
| Schema reason | source command accepted result |
| Schema changes | Externalize complete IReadOnlyList<Uuid>? to verified immutable content; preserve all items/order/identity, no truncation. |
| Schema reducerRule | Validate owner/generation and local predecessor; apply only this owner delta or immutable content reference. Never hydrate a foreign aggregate or mutate based on a projection. |
| Schema serializer | Register concrete target event and every proof/content variant in ComplianceCoreJsonContext and attributed persistence preflight coverage. |
| Schema nativeResponseRule | FirstResponse is non-null only on this logical command first native accepting fact; it references FirstResponseDescriptor. OperationResult is non-null on the declared terminal business fact and references MutationResultContent. Both use verified content and static concrete result serializers; neither is reconstructed from current state. |

#### Event fields

| Field | Type | Required |
| --- | --- | --- |
| TenantId | Uuid | true |
| EmailAddress | string | true |
| Affiliation | string | true |
| Administrator | bool | true |
| TokenHash | string | true |
| ExpiresAt | DateTimeOffset | true |
| InvitedBy | Uuid | true |
| BuiltInRole | string? | false |
| DeliveryAttemptId | Uuid? | false |
| TokenKeyId | string? | false |
| GuestProgramIdsManifest | ImmutableContentRef | false |
| GuestExpiresOn | DateOnly? | false |
| InvitedAt | DateTimeOffset | false |
| InvitedByDisplay | string? | false |
| Operation | OperationProvenance | true |
| FirstResponse | ImmutableContentRef? | true |
| OperationResult | ImmutableContentRef? | true |

#### Nested content schemas

#### Content schema: TenantMemberInvited.GuestProgramIdsManifest.v1

| Property | Value |
| --- | --- |
| field | GuestProgramIdsManifest |
| schema | TenantMemberInvited.GuestProgramIdsManifest.v1 |
| contentType | IReadOnlyList<Uuid>? |
| rule | Full exact original collection with complete count and digest; readers verify content before use. |


[Placeholder: no resolved intent schema is recorded.]

#### Choreography references

| Row | Relationship | Source owner | Receiver command | Target owner |
| --- | --- | --- | --- | --- |
| [T117](../reactors/tenant-invitation-delivery-process-reactor.md#t117) | physical root, logical prerequisite | TenantInvitation | SendTenantInvitation | TenantInvitationDeliveryProcess |
| [T118](../reactors/tenant-invitation-delivery-process-reactor.md#t118) | physical root, logical prerequisite | TenantInvitation | RecordTenantInvitationDeliverySent | TenantInvitationDeliveryProcess |
| [T119](../reactors/tenant-invitation-delivery-process-reactor.md#t119) | physical root, logical prerequisite | TenantInvitation | RecordTenantInvitationDeliveryFailed | TenantInvitationDeliveryProcess |
| [S031](../reactors/tenant-registration-reactor.md#s031) | emitted semantic fact | Tenant | InviteTenantMember | TenantInvitation |




[Placeholder: consumers and lifecycle ordering are not fully captured by the event inventory.]
