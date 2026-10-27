using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class CreateBoundaryHandler(BoundaryOperations operations, IBoundaryReferenceValidator references)
    : IRequestHandler<CreateBoundary, BoundaryRegistration>
{
    public ValueTask<Result<BoundaryRegistration>> HandleAsync(IRequestContext<CreateBoundary> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<CreateBoundary, BoundaryRegistration>(context, request.TenantId, request.ProgramId,
            request.BoundaryId, request.BoundaryId, nameof(CreateBoundary), null,
            async (snapshot, actor, at, token) =>
            {
                var program = await operations.Sources.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), token).ConfigureAwait(false);
                return !program.IsCreated ? Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."))
                    : await references.ValidateAsync(request.TenantId, request.ProgramId, request.Content, token).ConfigureAwait(false);
            },
            (rules, actor, at) =>
            {
                var result = rules.Create(request.ProgramId, Uuid.CreateVersion5(request.BoundaryId, "draft-1"), request.Content,
                    actor.MemberId, actor.Display, at);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
            }, null, null, ct);
    }
}
