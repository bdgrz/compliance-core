using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Only administrative display fields from the invitation stream: the latest invitation of a mailbox. The guest
///     scope stays a reference to its verified manifest and delivery state is read from the delivery process.
/// </summary>
public sealed record TenantInvitationDirectoryEntry(Uuid TenantId, Uuid InvitationId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, DateTimeOffset ExpiresAt,
    Uuid InvitedBy, Uuid? AcceptedUserId, ImmutableContentRef? GuestProgramIdsManifest = null,
    DateOnly? GuestExpiresOn = null);
