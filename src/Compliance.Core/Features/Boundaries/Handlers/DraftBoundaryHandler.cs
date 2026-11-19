using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class DraftBoundaryHandler(BoundaryOperations operations, IBoundaryReferenceValidator references)
    : IRequestHandler<DraftBoundary, BoundaryRegistration>
{
    public ValueTask<Result<BoundaryRegistration>> HandleAsync(IRequestContext<DraftBoundary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync<DraftBoundary, BoundaryRegistration>(context, request.TenantId,
            request.ProgramId, request.ProgramId, request.CommandId, nameof(DraftBoundary), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                var program = await operations.Sources.HydrateAsync(
                    new ComplianceProgram(request.TenantId, request.ProgramId), token).ConfigureAwait(false);
                if (!program.IsCreated)
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
                if (snapshot.Revision != request.ExpectedRevision)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The boundary changed. Reload its current revision."));
                return await references.ValidateAsync(request.TenantId, request.ProgramId, request.Content, token)
                    .ConfigureAwait(false);
            },
            (rules, actor, at) =>
            {
                var result = rules.DraftBoundary(request.ProgramId,
                    Uuid.CreateVersion5(request.CommandId, "draft"), request.ExpectedRevision,
                    request.Content, actor.MemberId, actor.Display, at);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
            }, null, null, ct);
    }
}
