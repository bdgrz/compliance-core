using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     An invitation was accepted by an authenticated user who proved the invited mailbox. The fact is the native
///     acceptance of the invitation's acceptance operation and freezes the exact terms the reaction provisions.
///     <paramref name="GuestProgramIdsManifest" /> references the complete ordered guest program scope.
/// </summary>
[Discriminator("bdgrz.tenant-invitation.accepted", 1)]
public sealed record TenantInvitationAccepted(Uuid TenantId, Uuid InvitationId, Uuid UserId, string EmailAddress,
    string Affiliation, bool Administrator, string? BuiltInRole, ImmutableContentRef? GuestProgramIdsManifest,
    DateOnly? GuestExpiresOn, DateTimeOffset AcceptedAt, Uuid InvitedBy, string? InvitedByDisplay,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
