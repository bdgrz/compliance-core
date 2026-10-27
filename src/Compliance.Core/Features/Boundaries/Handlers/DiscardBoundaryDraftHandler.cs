using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class DiscardBoundaryDraftHandler(BoundaryOperations operations) : IRequestHandler<DiscardBoundaryDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DiscardBoundaryDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, null, request.BoundaryId, request.CommandId,
            nameof(DiscardBoundaryDraft), request.ExpectedRevision,
            static (_, _, _, _) => BoundaryOperations.Ready(),
            (rules, actor, at) => BoundaryOperations.Outcome(rules.DiscardDraft(request.DraftVersionId, request.ExpectedRevision,
                request.Rationale, actor.MemberId, actor.Display, at)), null, null, ct);
    }
}
