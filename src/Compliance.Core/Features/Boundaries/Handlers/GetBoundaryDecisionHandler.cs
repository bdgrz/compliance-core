using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Features.Boundaries.Handlers;

public sealed class GetBoundaryDecisionHandler(IBoundaryDirectoryReader directory)
    : IRequestHandler<GetBoundaryDecision, BoundaryDecisionView>
{
    public async ValueTask<Result<BoundaryDecisionView>> HandleAsync(
        IRequestContext<GetBoundaryDecision> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            var decision = await directory.GetDecisionAsync(request.TenantId,
                request.BoundaryId, request.DecisionId, ct).ConfigureAwait(false);
            return decision is null
                ? Result<BoundaryDecisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The boundary decision was not found."))
                : Result<BoundaryDecisionView>.Success(decision);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<BoundaryDecisionView>.Failure(BoundaryReadFailure.Of(failure));
        }
    }
}
