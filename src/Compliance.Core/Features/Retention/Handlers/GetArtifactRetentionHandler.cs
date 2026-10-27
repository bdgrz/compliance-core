using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Retention.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
namespace Bdgrz.Compliance.Features.Retention.Handlers;

sealed class GetArtifactRetentionHandler(ArtifactRetentionRead read) : IRequestHandler<GetArtifactRetention, ArtifactRetentionView>
{
    public ValueTask<Result<ArtifactRetentionView>> HandleAsync(IRequestContext<GetArtifactRetention> context, CancellationToken ct)
    {
        var request = context.Request;
        return read.GetAsync(request.TenantId, request.SourceKind, request.SourceId, request.MinimumRevision, ct);
    }
}
