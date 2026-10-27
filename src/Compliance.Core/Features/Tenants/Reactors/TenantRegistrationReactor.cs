using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Reactors;

/// <summary>
///     Starts the slug claims a tenant fact causes, and invites the first administrator. The tenant fact already
///     records its owner, so no separate owner relationship is registered. Every claim is the exact logical
///     operation its source fact causes, so a replay reuses it.
/// </summary>
public sealed partial class TenantRegistrationReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantRegistered>,
      IReactorHandler<TenantSlugChangeRequested>,
      IReactorHandler<TenantSlugChanged>,
      IReactorHandler<TenantSlugSurrenderRequested>
{
    public async ValueTask HandleAsync(IReactorContext<TenantRegistered> context, CancellationToken ct)
    {
        await bus.SendReactionAsync(
            TenantSlugEffects.Register(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.Slug), context, ct);
        if (!context.Trigger.CreatorIsAdministrator &&
            context.Trigger.FirstAdministratorEmail is { } email)
            // The tenant fixes the invitation id, so a replay is an exact retry of the same invitation.
            await bus.SendReactionAsync(new InviteTenantMember(
                TenantInvitationIds.FirstAdministratorOf(context.Trigger.TenantId), context.Trigger.TenantId, email,
                "client_personnel", Administrator: true),
                context, ct);
    }

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Surrender(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChangeRequested> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Register(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.NewSlug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugChanged> context, CancellationToken ct) =>
        bus.SendReactionAsync(
            TenantSlugEffects.Surrender(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
                context.Trigger.OldSlug), context, ct);
}
