using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Cntryl.Portia;

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
