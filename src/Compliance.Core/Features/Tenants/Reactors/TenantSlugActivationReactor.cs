using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants.Reactors;

/// <summary>The slug can finish after primary designation, including recovery from a rejected first claim.</summary>
public sealed partial class TenantSlugActivationReactor(IProjectionCheckpointStore checkpoints,
    IAggregateReader reader, IRequestBus bus, ILogger<TenantSlugActivationReactor> logger,
    TenantActivationPolicy? policy = null)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "tenants")),
      IReactorHandler<TenantSlugConfirmed>, IReactorHandler<TenantSlugChanged>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask<Result> HandleAsync(IReactorContext<TenantSlugConfirmed> context, CancellationToken ct) =>
        ReactAsync(context, context.Trigger.TenantId, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<TenantSlugChanged> context, CancellationToken ct) =>
        ReactAsync(context, context.Trigger.TenantId, ct);

    async ValueTask<Result> ReactAsync<TEvent>(IReactorContext<TEvent> context, Uuid tenantId, CancellationToken ct)
        where TEvent : DomainEvent
    {
        var tenant = await reader.HydrateAsync(new Tenant(tenantId), ct).ConfigureAwait(false);
        if (!tenant.CanActivateCreator)
            return Result.Success;
        var member = await reader.HydrateAsync(new Member(tenantId, tenant.OwnerUserId), ct).ConfigureAwait(false);
        var primaries = await reader.HydrateAsync(new PrimaryOrgAdmins(tenantId), ct).ConfigureAwait(false);
        if (!member.IsPrimary || !primaries.HasDesignated(tenant.OwnerUserId, member.MembershipEpisodeId,
                member.PrimaryDesignationId))
            return Result.Success;
        return await _policy.ReactAsync(nameof(TenantSlugActivationReactor), tenantId, context.Trigger, logger,
            token => _policy.SendActivationAsync(bus,
                TenantActivationEffects.Activate(context.Trigger.Metadata.EventId, tenantId, tenant.OwnerUserId),
                context, logger, token), ct).ConfigureAwait(false);
    }
}
