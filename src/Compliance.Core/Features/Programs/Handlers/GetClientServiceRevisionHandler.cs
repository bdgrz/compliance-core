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

public sealed class GetClientServiceRevisionHandler(IClientServiceDirectoryReader directory,
    ClientServiceHistoryReadConsistency consistency)
    : IRequestHandler<GetClientServiceRevision, ClientServiceRevisionView>
{
    public async ValueTask<Result<ClientServiceRevisionView>> HandleAsync(
        IRequestContext<GetClientServiceRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ClientServiceRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId, request.ServiceId,
            request.Revision, ct).ConfigureAwait(false);
        return revision is null
            ? Result<ClientServiceRevisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service revision was not found."))
            : Result<ClientServiceRevisionView>.Success(revision);
    }
}
