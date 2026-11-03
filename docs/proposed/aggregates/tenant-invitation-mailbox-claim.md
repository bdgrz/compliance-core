# TenantInvitationMailboxClaim

- Classification: `business_identity_or_uniqueness_authority`
- Source area: `Tenants` (derived from source path; not a bounded-context ruling).
- Business aggregate: `true`
- Selected status: `SELECTED_TARGET_DEFINITION_NO_IMPLEMENTATION_AUTHORIZED`


## Identity and invariant

**Selected identity:** Tenant-scoped tenant / normalizedEmail; `TenantInvitationIds.AcceptanceOf(InvitationId)` on `AcceptTenantInvitation` is the caller-supplied immutable creation identity (recorded from source 2026-10-29) so an exact retry resolves the original result and a different id is a different record; later mutations use CommandId bound to this identity and the expected revision.

**Invariant:** At most one pending invitation per mailbox. InvitationId and generation live in state and every release compares both; generation is never part of the stream key.

**Additional decision rule:** [Placeholder: no owner-specific decision rule is recorded.]

**Must preserve:** Retry same invitation ID with exact terms returns original acceptance; changed terms conflict. New invitation generation is independent. Delivery retries preserve exact token and accepted at-least-once policy.

**Replay:** Current required authorization precedes replay; exact authorized retries resolve the original accepted result using the selected logical identity and immutable actor/intent/source binding. Fresh mutable feasibility, clock selection and body preparation cannot replace that original result. Expected revision is concurrency precondition, not a new identity.

**Lifecycle:** [Placeholder: a consolidated lifecycle sequence is not present in the selected owner record.]





## Commands

| Command | Kind | Identity field |
| --- | --- | --- |
| [InviteTenantMember](../commands/tenant-invitation-mailbox-claim.md#invitetenantmember) | http | InvitationId |
| [InviteOrganizationMember](../commands/tenant-invitation-mailbox-claim.md#inviteorganizationmember) | http | InvitationId |
| [InviteGuestConsultant](../commands/tenant-invitation-mailbox-claim.md#inviteguestconsultant) | http | InvitationId |
| [AcceptTenantInvitation](../commands/tenant-invitation-mailbox-claim.md#accepttenantinvitation) | http | TenantInvitationIds.AcceptanceOf(InvitationId) |

## Events

| Event | Discriminator | Version |
| --- | --- | --- |
| [TenantInvitationMailboxClaimed](../events/tenant-invitation-mailbox-claim.md#tenantinvitationmailboxclaimed) | bdgrz.tenant-invitation-mailbox.claimed | 1 |
| [TenantInvitationMailboxReleased](../events/tenant-invitation-mailbox-claim.md#tenantinvitationmailboxreleased) | bdgrz.tenant-invitation-mailbox.released | 1 |

## Cross-owner flows

[Placeholder: no choreography row names this owner.]

## Product rules

[Placeholder: product-rule traceability is not recorded for this owner.]

## Adopted architecture decisions

[Placeholder: no adopted architecture-decision reference is recorded.]

## Evidence and complete record

- Baseline source: [Placeholder: source file is not present in this checkout]
- Aggregate review: [Placeholder: no owner-specific review file is present]
- Full selected record: [JSON](../data/aggregates/tenant-invitation-mailbox-claim.json)

> Target definition is separate from runtime qualification. Implementation status recorded by the source: `SEPARATE_VERIFICATION_REQUIRED_IMPLEMENTATION_HALTED`.
