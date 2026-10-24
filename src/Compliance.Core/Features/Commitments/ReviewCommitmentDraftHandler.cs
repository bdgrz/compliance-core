using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ReviewCommitmentDraftHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ReviewCommitmentDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewCommitmentDraft> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Commitment sign-off requires personal HTTP submission."));
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var operation = AuthoredCommandReplay.Create(new CommitmentDraft(request.TenantId, request.DraftId, request.CommandId), request.CommandId,
            nameof(ReviewCommitmentDraft), RbacIds.Member(request.TenantId, userId), request.ExpectedRevision, request, clock.GetUtcNow());
        var replayOwner = await reader.HydrateAsync(new CommitmentDraft(request.TenantId, request.DraftId, request.CommandId), ct).ConfigureAwait(false);
        if (AuthoredCommandReplay.Unit(replayOwner.ReplayAcceptance, operation) is { } replay)
            return replay;

        SeparationOfDutiesWaiver? waiver = null;
        if (request.SeparationOfDutiesWaiverId is { } waiverId)
            waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId,
                waiverId), ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.DraftId, request.CommandId),
            draft => CommandFailureRequestAdapter.ToOutcome(draft.Review(request.ProgramId,
                request.ExpectedRevision, request.CommandId, request.Outcome,
                request.OwnerReference, request.Applicability, request.Interpretation,
                request.InterpretationNote, request.Rationale,
                RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(),
                waiver, request.SourceVerifiedReference, request.SourceEvidence, operation: operation)),
            context, ct).ConfigureAwait(false);
    }
}
