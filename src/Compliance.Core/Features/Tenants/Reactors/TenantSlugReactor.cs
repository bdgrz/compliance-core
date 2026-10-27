using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Carries each answer of the slug owner back to the tenant it answers. Every answer is the exact logical
///     operation its source fact causes; the tenant treats a replayed or late answer as a terminal no-op, so this
///     reactor never fails on a stale outcome and the global checkpoint always advances.
/// </summary>
public sealed partial class TenantSlugReactor(
    IProjectionCheckpointStore checkpoints,
    IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenant-slugs")),
      IReactorHandler<TenantSlugRegistered>,
      IReactorHandler<TenantSlugRegistrationRejected>,
      IReactorHandler<TenantSlugSurrendered>,
      IReactorHandler<TenantSlugSurrenderRejected>
{
    public ValueTask HandleAsync(IReactorContext<TenantSlugRegistered> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.Confirm(context.Trigger.Metadata.EventId, context.Trigger.TenantId,
            context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(
        IReactorContext<TenantSlugRegistrationRejected> context,
        CancellationToken ct) => bus.SendReactionAsync(TenantSlugEffects.Reject(context.Trigger.Metadata.EventId,
        context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrendered> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.ConfirmSurrender(context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.Slug), context, ct);

    public ValueTask HandleAsync(IReactorContext<TenantSlugSurrenderRejected> context, CancellationToken ct) =>
        bus.SendReactionAsync(TenantSlugEffects.RejectSurrender(context.Trigger.Metadata.EventId,
            context.Trigger.TenantId, context.Trigger.Slug), context, ct);
}
