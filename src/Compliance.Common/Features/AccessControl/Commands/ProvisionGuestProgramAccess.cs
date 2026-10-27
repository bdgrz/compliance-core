using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Internal invitation reaction that applies the inviter-approved guest program scope.</summary>
[Discriminator("bdgrz.access-grant.guest-invitation.provision", 1)]
public sealed record ProvisionGuestProgramAccess(Uuid TenantId, Uuid UserId, Uuid ProgramId,
    Uuid InvitationId, DateOnly ExpiresOn, DateTimeOffset AcceptedAt,
    Uuid InvitedBy, string InvitedByDisplay) : IRequest;
