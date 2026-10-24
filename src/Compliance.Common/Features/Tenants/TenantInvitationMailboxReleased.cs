using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The invitation that held the mailbox in <paramref name="Generation" /> no longer does, which lets a later
///     invitation claim it. A release names the exact invitation and generation it ends.
/// </summary>
[Discriminator("bdgrz.tenant-invitation-mailbox.released", 1)]
public sealed record TenantInvitationMailboxReleased(Uuid TenantId, string EmailAddress, Uuid InvitationId,
    long Generation, string Reason, DateTimeOffset ReleasedAt) : DomainEvent;
