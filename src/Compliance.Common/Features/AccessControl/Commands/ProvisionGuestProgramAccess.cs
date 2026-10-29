using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>Internal invitation reaction that applies the inviter-approved guest program scope.</summary>
[Discriminator("bdgrz.access-grant.guest-invitation.provision", 1)]
public sealed record ProvisionGuestProgramAccess(Uuid TenantId, Uuid UserId, Uuid ProgramId,
    Uuid InvitationId, DateOnly ExpiresOn, DateTimeOffset AcceptedAt,
    Uuid InvitedBy, string InvitedByDisplay) : IRequest;
