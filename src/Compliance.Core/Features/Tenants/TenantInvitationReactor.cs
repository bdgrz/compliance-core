using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed partial class TenantInvitationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus,
    IImmutableContentStore content, ILogger<TenantInvitationReactor> logger, TenantActivationPolicy? policy = null)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations")),
      IReactorHandler<TenantInvitationAccepted>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask HandleAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct) =>
        _policy.ReactAsync(nameof(TenantInvitationReactor), context.Trigger.TenantId, context.Trigger,
            logger, token => ReactAsync(context, token), ct);

    async ValueTask ReactAsync(IReactorContext<TenantInvitationAccepted> context, CancellationToken ct)
    {
        var accepted = context.Trigger;
        var invitedByDisplay = accepted.InvitedByDisplay;
        IReadOnlyList<Uuid>? guestPrograms = null;
        DateOnly? guestExpiresOn = null;
        if (accepted.Affiliation == "guest")
        {
            if (accepted.GuestProgramIdsManifest is null || accepted.GuestExpiresOn is not { } expiresOn ||
                accepted.InvitedBy == Uuid.Empty || string.IsNullOrWhiteSpace(invitedByDisplay) ||
                accepted.AcceptedAt == default ||
                DateOnly.FromDateTime(accepted.AcceptedAt.UtcDateTime) > expiresOn)
                throw new InvalidOperationException("A guest acceptance must retain its invited scope and attribution.");
            guestPrograms = await content.ReadJsonAsync<IReadOnlyList<Uuid>>(accepted.TenantId.ToString(),
                accepted.GuestProgramIdsManifest, ct).ConfigureAwait(false);
            if (guestPrograms is not { Count: > 0 })
                throw new InvalidOperationException("A guest acceptance must retain its invited scope and attribution.");
            guestExpiresOn = expiresOn;
        }

        // A user re-invited while already a live member keeps that episode; the owner reports the exact
        // live-episode conflict and the rest of the acceptance (grants, teams, activation) still applies. The
        // registration id is the episode id, so a replay binds the team assignments to the episode it registered.
        var registration = MemberRegistrationEffects.Register(accepted.Metadata.EventId, accepted.TenantId,
            accepted.UserId, accepted.Affiliation, guestExpiresOn);
        var registered = await bus.SendAsync(registration, context, ct).ConfigureAwait(false);
        if (!registered.IsSuccess && registered.Error != Member.LiveEpisodeConflict)
            throw new ReactionCommandFailedException(registered.Error);
        var episodeId = registered.IsSuccess ? registration.CommandId : Uuid.Empty;
        if (guestPrograms is not null && guestExpiresOn is { } endDate)
        {
            foreach (var programId in guestPrograms)
            {
                await bus.SendReactionAsync(new ProvisionGuestProgramAccess(accepted.TenantId, accepted.UserId,
                    programId, accepted.InvitationId, endDate, accepted.AcceptedAt, accepted.InvitedBy,
                    invitedByDisplay!), context, ct);
            }
        }

        var memberId = RbacIds.Member(accepted.TenantId, accepted.UserId);
        if (accepted.BuiltInRole is { } role)
        {
            var teamId = BuiltInRbac.TeamIdForRole(accepted.TenantId, role) ??
                throw new InvalidOperationException("The accepted invitation has an unknown built-in role.");
            await AssignAsync(context, teamId, memberId, episodeId, ct).ConfigureAwait(false);
        }

        if (accepted.Administrator)
        {
            // The administrators team is admitted on the tenant manager guard; the guard's process applies it.
            await AssignAsync(context, BuiltInRbac.AdministratorsTeamId(accepted.TenantId), memberId, episodeId, ct)
                .ConfigureAwait(false);
            await _policy.SendActivationAsync(bus,
                TenantActivationEffects.Activate(accepted.Metadata.EventId, accepted.TenantId, accepted.UserId, accepted.EmailAddress), context, logger, ct);
        }
    }

    // A user re-invited while already on the team keeps that assignment; the owner reports the exact
    // already-assigned conflict and the rest of the acceptance still applies.
    async ValueTask AssignAsync(IReactorContext<TenantInvitationAccepted> context, Uuid teamId, Uuid memberId,
        Uuid episodeId, CancellationToken ct)
    {
        var assignment = await bus.SendAsync(TeamMemberEffects.Assign(context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, teamId, memberId, episodeId), context, ct).ConfigureAwait(false);
        if (!assignment.IsSuccess && assignment.Error != TeamMember.AlreadyAssignedConflict)
            throw new ReactionCommandFailedException(assignment.Error);
    }
}
