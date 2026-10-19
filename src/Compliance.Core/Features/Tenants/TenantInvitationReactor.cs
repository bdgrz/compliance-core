using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    ILogger<TenantInvitationReactor> logger, TenantActivationPolicy? policy = null)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations")),
      IReactorHandler<TenantInvitationAccepted>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask HandleAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct) =>
        _policy.ReactAsync(nameof(TenantInvitationReactor), context.Trigger.TenantId, context.Trigger,
            logger, token => ReactAsync(context, token), ct);

    async ValueTask ReactAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct)
    {
        var invitedByDisplay = context.Trigger.InvitedByDisplay;
        Uuid[]? guestPrograms = null;
        DateOnly? guestExpiresOn = null;
        if (context.Trigger.Affiliation == "guest")
        {
            if (context.Trigger.GuestProgramIds is not { Count: > 0 } programs ||
                context.Trigger.GuestExpiresOn is not { } expiresOn ||
                context.Trigger.InvitedBy == Uuid.Empty ||
                string.IsNullOrWhiteSpace(invitedByDisplay) ||
                context.Trigger.AcceptedAt == default ||
                DateOnly.FromDateTime(context.Trigger.AcceptedAt.UtcDateTime) > expiresOn)
                throw new InvalidOperationException("A guest acceptance must retain its invited scope and attribution.");
            guestPrograms = programs.ToArray();
            guestExpiresOn = expiresOn;
        }

        await bus.SendReactionAsync(new RegisterMember(context.Trigger.TenantId,
            context.Trigger.UserId, context.Trigger.Affiliation, guestExpiresOn), context, ct);
        if (guestPrograms is not null && guestExpiresOn is { } endDate)
        {
            var invitationId = TenantInvitation.IdFor(context.Trigger.TenantId,
                context.Trigger.EmailAddress);
            foreach (var programId in guestPrograms)
            {
                await bus.SendReactionAsync(new ProvisionGuestProgramAccess(context.Trigger.TenantId,
                    context.Trigger.UserId, programId, invitationId, endDate,
                    context.Trigger.AcceptedAt, context.Trigger.InvitedBy,
                    invitedByDisplay!), context, ct);
            }
        }
        var memberId = RbacIds.Member(context.Trigger.TenantId, context.Trigger.UserId);
        if (context.Trigger.BuiltInRole is { } role)
        {
            var teamId = BuiltInRbac.TeamIdForRole(context.Trigger.TenantId, role) ??
                throw new InvalidOperationException("The accepted invitation has an unknown built-in role.");
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                teamId, memberId), context, ct);
        }
        if (context.Trigger.Administrator)
        {
            await bus.SendReactionAsync(new AssignTeamMember(context.Trigger.TenantId,
                BuiltInRbac.AdministratorsTeamId(context.Trigger.TenantId), memberId), context, ct);
            await _policy.SendActivationAsync(bus,
                new ActivateTenant(context.Trigger.TenantId, context.Trigger.UserId,
                    context.Trigger.EmailAddress), context, logger, ct);
        }
    }
}
