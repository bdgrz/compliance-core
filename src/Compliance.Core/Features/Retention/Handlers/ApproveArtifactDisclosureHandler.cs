using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class ApproveArtifactDisclosureHandler(ArtifactDisclosureAccess access,
    IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ApproveArtifactDisclosure>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApproveArtifactDisclosure> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!PersonalHttpActor(context.Actor, context.Invocation, request.TenantId, out var actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Disclosure approval requires a personal HTTP decision."));
        var allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        var source = await ArtifactDisclosureSourceReader.LoadAsync(reader, request.TenantId,
            request.ArtifactId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        if (source.Value.ArtifactRevision != request.ArtifactRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact changed before disclosure approval."));
        var fence = await ArtifactDisclosureSourceReader.CheckAsync(reader, source.Value, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return fence;
        allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        return await executor.ExecuteAsync(new ArtifactDisclosure(request.TenantId, request.DisclosureId),
            aggregate => AggregateOutcome.CommitOnSuccess(aggregate.Approve(source.Value, request.Recipient,
                request.Purpose, request.Rationale, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }

    static bool PersonalHttpActor(System.Security.Claims.ClaimsPrincipal principal,
        RequestInvocation invocation, Uuid tenantId, out ActorReference actor)
    {
        actor = null!;
        if (invocation is not HttpInvocation || RequestActor.IsSystem(principal) ||
            !UserIdentityClaims.TryGetBdgrzSubject(principal, out var user))
            return false;
        actor = ActorReference.ForMember(RbacIds.Member(tenantId, user),
            UserIdentityClaims.BdgrzDisplay(principal, user));
        return true;
    }
}
