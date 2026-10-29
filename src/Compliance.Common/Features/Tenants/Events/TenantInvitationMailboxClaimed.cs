using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.Tenants.Events;

/// <summary>
///     One invitation holds the tenant mailbox as its single pending invitation. The generation increases with
///     every claim and is state, never part of the stream key.
/// </summary>
[Discriminator("bdgrz.tenant-invitation-mailbox.claimed", 1)]
public sealed record TenantInvitationMailboxClaimed(Uuid TenantId, string EmailAddress, Uuid InvitationId,
    long Generation, DateTimeOffset ClaimedAt) : DomainEvent;
