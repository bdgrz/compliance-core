# TenantInvitation

- Classification: `business_aggregate`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped invitationId; `InvitationId` on `AcceptTenantInvitation` is the caller-supplied immutable creation identity (one immutable authored operation) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** One immutable offer, token acceptance and terminal lifecycle; each invitation has exact recipient, purpose, scopes and expiry.

**Additional decision rule:** An invitation lives 7 days and is accepted only when the invitee signs in through a trusted issuer with the invited verified email address; affiliation is client_personnel, firm_staff or guest, a guest names at least one distinct program, and the first administrator must be client personnel.

**Must preserve:** Retry same invitation ID with exact terms returns original acceptance; changed terms conflict. New invitation generation is independent. Delivery retries preserve exact token and accepted at-least-once policy.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [AcceptTenantInvitation](../commands/tenant-invitation.md#accepttenantinvitation) | http | InvitationId |
| [InviteGuestConsultant](../commands/tenant-invitation.md#inviteguestconsultant) | http | InvitationId |
| [InviteOrganizationMember](../commands/tenant-invitation.md#inviteorganizationmember) | http | InvitationId |
| [InviteTenantMember](../commands/tenant-invitation.md#invitetenantmember) | http | InvitationId |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantInvitationAccepted](../events/tenant-invitation.md#tenantinvitationaccepted) | bdgrz.tenant-invitation.accepted | 1 |
| [TenantMemberInvited](../events/tenant-invitation.md#tenantmemberinvited) | bdgrz.tenant-member.invited | 1 |

## Cross-owner flows

| Row | Kind | Source owner | Target owner | Command |
| --- | --- | --- | --- | --- |
| [T117](../reactors/invite-member-to-join-tenant-reactor.md#t117) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | SendTenantInvitation |
| [T118](../reactors/invite-member-to-join-tenant-reactor.md#t118) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | RecordTenantInvitationDeliverySent |
| [T119](../reactors/invite-member-to-join-tenant-reactor.md#t119) | reaction_or_regular_process_stage | TenantInvitation | TenantInvitationDeliveryProcess | RecordTenantInvitationDeliveryFailed |
| [S026](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s026) | business_receiver | TenantInvitation | Member | RegisterMember |
| [S027](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s027) | business_receiver | TenantInvitation | TeamMember | AssignTeamMember |
| [S028](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s028) | business_receiver | TenantInvitation | AccessGrant | ProvisionGuestProgramAccess |
| [S029](../reactors/activate-membership-after-invitation-acceptance-reactor.md#s029) | business_receiver | TenantInvitation | Tenant | ActivateTenant |
| [S031](../reactors/establish-tenant-and-invite-owner-reactor.md#s031) | business_receiver | Tenant | TenantInvitation | InviteTenantMember |

## Product rules

- **D03-10** — An invitation is recorded by the platform and is accepted only when the invitee signs in through a trusted issuer with the invited, verified email address. Decision: [m0-d03-roles-and-separation-of-duties.md](../decisions/m0-d03-roles-and-separation-of-duties.md).

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant-invitation.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
