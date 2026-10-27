using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class GetApplicationImportRetentionHandler(ArtifactRetentionRead read) : IRequestHandler<GetApplicationImportRetention, ArtifactRetentionView>
{
    public ValueTask<Result<ArtifactRetentionView>> HandleAsync(IRequestContext<GetApplicationImportRetention> context, CancellationToken ct)
    {
        var request = context.Request;
        return read.GetAsync(request.TenantId, "application_import", request.BatchId, request.MinimumRevision, ct);
    }
}
