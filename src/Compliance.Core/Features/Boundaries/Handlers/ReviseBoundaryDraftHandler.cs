using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Boundaries.Commands;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

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
