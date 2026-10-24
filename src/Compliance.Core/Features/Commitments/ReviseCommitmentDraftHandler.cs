using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class ReviseCommitmentDraftHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<ReviseCommitmentDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseCommitmentDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var operation = AuthoredCommandReplay.Create(new CommitmentDraft(request.TenantId, request.DraftId, request.CommandId), request.CommandId,
            nameof(ReviseCommitmentDraft), RbacIds.Member(request.TenantId, userId), request.ExpectedRevision, request, clock.GetUtcNow());

        return executor.ExecuteAsync(new CommitmentDraft(request.TenantId, request.DraftId, request.CommandId),
            draft => CommandFailureRequestAdapter.ToOutcome(draft.Revise(request.ProgramId,
                request.ExpectedRevision, request.Statement, request.Context,
                request.SourceReference, RbacIds.Member(request.TenantId, userId),
                UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow(), operation: operation)),
            context, ct);
    }
}
