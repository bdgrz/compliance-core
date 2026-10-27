using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
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
