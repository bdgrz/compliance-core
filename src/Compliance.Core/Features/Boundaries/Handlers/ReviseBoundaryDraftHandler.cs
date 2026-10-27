using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed class ReviseBoundaryDraftHandler(BoundaryOperations operations, IBoundaryReferenceValidator references)
    : IRequestHandler<ReviseBoundaryDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseBoundaryDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, null, request.BoundaryId, request.CommandId,
            nameof(ReviseBoundaryDraft), request.ExpectedRevision,
            (snapshot, actor, at, token) => references.ValidateAsync(request.TenantId, snapshot.ProgramId, request.Content, token),
            (rules, actor, at) => BoundaryOperations.Outcome(rules.Revise(request.DraftVersionId, request.ExpectedRevision,
                request.Content, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
