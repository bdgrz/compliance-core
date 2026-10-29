using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class GetRiskDraftRevisionHandler(IRiskDraftDirectoryReader directory,
    RiskDraftReadConsistency consistency)
    : IRequestHandler<GetRiskDraftRevision, RiskDraftRevisionView>
{
    public async ValueTask<Result<RiskDraftRevisionView>> HandleAsync(
        IRequestContext<GetRiskDraftRevision> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            if (request.Revision < 1)
                return Result<RiskDraftRevisionView>.Failure(new RequestError(
                    RequestErrorKind.Validation, "The risk draft revision must be positive."));
            var freshness = await consistency.GetAsync(request.TenantId, request.ProgramId,
                request.RiskId, request.Revision, ct).ConfigureAwait(false);
            if (!freshness.IsSuccess)
                return Result<RiskDraftRevisionView>.Failure(freshness.Error);
            var revision = await directory.GetRevisionAsync(request.TenantId, request.RiskId,
                request.Revision, ct).ConfigureAwait(false);
            return revision is not null && revision.TenantId == request.TenantId &&
                   revision.ProgramId == request.ProgramId && revision.RiskId == request.RiskId && revision.Revision == request.Revision
                ? Result<RiskDraftRevisionView>.Success(revision)
                : Result<RiskDraftRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The risk draft revision projection is incomplete.", isTransient: true));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<RiskDraftRevisionView>.Failure(RiskDraftReadFailure.Of(failure));
        }
    }
}
