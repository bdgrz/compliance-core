using Cntryl.Portia;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Reactors;

public sealed partial class TenantSelfServiceActivationReactor(IProjectionCheckpointStore checkpoints,
    IAggregateReader reader, IRequestBus bus, ILogger<TenantSelfServiceActivationReactor> logger,
    TenantActivationPolicy? policy = null)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("primary-org-admins")),
      IReactorHandler<PrimaryOrgAdminDesignated>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask<Result> HandleAsync(IReactorContext<PrimaryOrgAdminDesignated> context, CancellationToken ct) =>
        _policy.ReactAsync(nameof(TenantSelfServiceActivationReactor), context.Trigger.TenantId,
            context.Trigger, logger, token => ReactAsync(context, token), ct);

    async ValueTask<Result> ReactAsync(IReactorContext<PrimaryOrgAdminDesignated> context, CancellationToken ct)
    {
        var assignment = context.Trigger;
        var tenant = await reader.HydrateAsync(new Tenant(assignment.TenantId), ct);
        if (!tenant.CanActivateCreator ||
            assignment.UserId != tenant.OwnerUserId)
            return Result.Success;
        var reactionOutcome1 = await _policy.SendActivationAsync(bus,
            TenantActivationEffects.Activate(assignment.Metadata.EventId, assignment.TenantId, tenant.OwnerUserId), context, logger, ct);
        if (!reactionOutcome1.IsSuccess)
            return reactionOutcome1;
        return Result.Success;
    }
}
