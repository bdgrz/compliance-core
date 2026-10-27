using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class RecordEngagementReportIssuedHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordEngagementReportIssued>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordEngagementReportIssued> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Report issuance requires an attributed personal HTTP decision.")));

        var request = context.Request;
        var now = clock.GetUtcNow();
        if (request.IssuedAt > now)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A report cannot be recorded as issued in the future.")));
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return executor.ExecuteAsync(new ArtifactEngagementHold(request.TenantId, request.HoldId), hold =>
            AggregateOutcome.CommitOnSuccess(hold.RecordReportIssued(request.EngagementId,
                request.ReportArtifactId, request.IssuerReference, actor, request.IssuedAt)), context, ct);
    }
}
