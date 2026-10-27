using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class PlaceArtifactEngagementHoldHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<PlaceArtifactEngagementHold>
{
    public ValueTask<Result> HandleAsync(IRequestContext<PlaceArtifactEngagementHold> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Engagement holds require a personal HTTP decision.")));

        var request = context.Request;
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return executor.ExecuteAsync(new ArtifactEngagementHold(request.TenantId, request.HoldId), hold =>
            AggregateOutcome.CommitOnSuccess(hold.Place(request.EngagementId, request.ReportArtifactId,
                request.Scope, request.Authority, request.Reason, actor, clock.GetUtcNow())), context, ct);
    }
}
