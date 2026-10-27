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

sealed class RecordArtifactDisclosureDeliveryHandler(ArtifactDisclosureAccess access,
    IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RecordArtifactDisclosureDelivery>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RecordArtifactDisclosureDelivery> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!PersonalHttpActor(context.Actor, context.Invocation, request.TenantId, out var actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Disclosure delivery requires a personal HTTP decision."));
        var allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        var source = await ArtifactDisclosureSourceReader.LoadAsync(reader, request.TenantId,
            request.ArtifactId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        var now = clock.GetUtcNow();
        if (request.DeliveredAt == default || request.DeliveredAt > now)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The delivery time must be a recorded time no later than now."));
        var fence = await ArtifactDisclosureSourceReader.CheckAsync(reader, source.Value, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return fence;
        allowed = await access.RequireAsync(context, request.TenantId, request.ArtifactId, ct)
            .ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return allowed;
        return await executor.ExecuteAsync(new ArtifactDisclosure(request.TenantId,
                request.DisclosureId), aggregate => AggregateOutcome.CommitOnSuccess(
                aggregate.Deliver(source.Value, request.ExpectedRevision, request.DeliveryReference,
                    actor, request.DeliveredAt)), context, ct).ConfigureAwait(false);
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
