using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Records the acting member's own review; HTTP-only, never an MCP tool.</summary>
public sealed class ReviewPolicyDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewPolicyDraft, PolicyDecisionView>
{
    public async ValueTask<Result<PolicyDecisionView>> HandleAsync(
        IRequestContext<ReviewPolicyDraft> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<PolicyDecisionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Policy sign-off requires personal HTTP submission."));
        var request = context.Request;
        var actor = PolicyActor.From(context, request.TenantId);
        var operation = AuthoredCommandReplay.Create(new Policy(request.TenantId, request.PolicyId, request.CommandId), request.CommandId,
            nameof(ReviewPolicyDraft), actor.MemberId, request.ExpectedRevision, request, clock.GetUtcNow());
        var replayOwner = await reader.HydrateAsync(new Policy(request.TenantId, request.PolicyId, request.CommandId), ct).ConfigureAwait(false);
        if (AuthoredCommandReplay.TryReplay(replayOwner.ReplayAcceptance, operation, out var replayFailure))
            return replayFailure is null ? Result<PolicyDecisionView>.Success(replayOwner.FindDecision(request.CommandId)!)
                : Result<PolicyDecisionView>.Failure(replayFailure.ToRequestError());

        var waiver = await PolicySource.WaiverAsync(reader, request.TenantId,
            request.SeparationOfDutiesWaiverId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new Policy(request.TenantId, request.PolicyId, request.CommandId),
            policy =>
            {
                var failure = policy.Review(request.ProgramId, request.ExpectedRevision,
                    request.CommandId, request.Outcome, request.Rationale, actor.Reference,
                    actor.MemberId, clock.GetUtcNow(), waiver, operation: operation);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    policy.FindDecision(request.CommandId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
