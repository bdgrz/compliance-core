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
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-team-members")),
      IReactorHandler<TeamMemberAssigned>
{
    readonly TenantActivationPolicy _policy = policy ?? TenantActivationPolicy.Default;

    public ValueTask<Result> HandleAsync(IReactorContext<TeamMemberAssigned> context, CancellationToken ct) =>
        _policy.ReactAsync(nameof(TenantSelfServiceActivationReactor), context.Trigger.TenantId,
            context.Trigger, logger, token => ReactAsync(context, token), ct);

    async ValueTask<Result> ReactAsync(IReactorContext<TeamMemberAssigned> context, CancellationToken ct)
    {
        var assignment = context.Trigger;
        if (assignment.TeamId != BuiltInRbac.AdministratorsTeamId(assignment.TenantId))
            return Result.Success;

        var tenant = await reader.HydrateAsync(new Tenant(assignment.TenantId), ct);
        if (!tenant.NeedsCreatorActivation ||
            assignment.MemberId != RbacIds.Member(assignment.TenantId, tenant.OwnerUserId))
            return Result.Success;
        var reactionOutcome1 = await _policy.SendActivationAsync(bus,
            TenantActivationEffects.Activate(assignment.Metadata.EventId, assignment.TenantId, tenant.OwnerUserId), context, logger, ct);
        if (!reactionOutcome1.IsSuccess)
            return reactionOutcome1;
        return Result.Success;
    }
}
