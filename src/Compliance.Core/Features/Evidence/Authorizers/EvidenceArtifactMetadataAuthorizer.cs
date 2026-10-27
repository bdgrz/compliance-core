using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Authorizers;

sealed class EvidenceArtifactMetadataAuthorizer(EvidenceArtifactReadAccess access)
    : IRequestAuthorizer<GetEvidenceArtifactMetadata>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<GetEvidenceArtifactMetadata> context, CancellationToken ct) =>
        access.RequireAsync(context, context.Request.TenantId, context.Request.ArtifactId, ct);
}
