using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class ProposeBoundarySuccessorHandler(BoundaryOperations operations, IBoundaryReferenceValidator references)
    : IRequestHandler<ProposeBoundarySuccessor, BoundaryRegistration>
{
    public ValueTask<Result<BoundaryRegistration>> HandleAsync(IRequestContext<ProposeBoundarySuccessor> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<ProposeBoundarySuccessor, BoundaryRegistration>(context, request.TenantId, null,
            request.BoundaryId, request.CommandId, nameof(ProposeBoundarySuccessor), request.ExpectedRevision,
            async (snapshot, actor, at, token) => snapshot.Revision != request.ExpectedRevision
                ? Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("boundary", snapshot.Revision)).ToRequestError())
                : await references.ValidateAsync(request.TenantId, snapshot.ProgramId, request.Content, token).ConfigureAwait(false),
            (rules, actor, at) => BoundaryOperations.Outcome(rules.ProposeSuccessor(request.ExpectedApprovedVersionId,
                Uuid.CreateVersion5(request.CommandId, "draft"), request.Content, actor.MemberId, actor.Display, at, request.CommandId)), null, null, ct);
    }
}
