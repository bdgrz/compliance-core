using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Queries;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class GetProgramRevisionHandler(IProgramDirectoryReader directory,
    ProgramHistoryReadConsistency consistency)
    : IRequestHandler<GetProgramRevision, ProgramRevisionView>
{
    public async ValueTask<Result<ProgramRevisionView>> HandleAsync(
        IRequestContext<GetProgramRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ProgramRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ProgramId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ProgramRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program revision was not found."))
            : Result<ProgramRevisionView>.Success(revision);
    }
}
