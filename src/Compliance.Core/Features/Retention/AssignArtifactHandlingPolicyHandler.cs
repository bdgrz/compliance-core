using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class AssignArtifactHandlingPolicyHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<AssignArtifactHandlingPolicy>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignArtifactHandlingPolicy> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Handling policy decisions require a personal HTTP invocation."));

        var request = context.Request;
        var source = await EvidenceHandlingPolicySourceReader.LoadAsync(reader, request.TenantId,
            request.ArtifactId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        var current = await EvidenceHandlingPolicySourceReader.CheckAsync(reader, source.Value, ct)
            .ConfigureAwait(false);
        if (!current.IsSuccess)
            return current;

        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        return await executor.ExecuteAsync(new EvidenceHandlingPolicy(request.TenantId,
                request.ArtifactId), policy => AggregateOutcome.CommitOnSuccess(policy.Assign(
                source.Value, request.ExpectedRevision, request.HandlingClass, request.PolicyVersion,
                request.Rationale, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
