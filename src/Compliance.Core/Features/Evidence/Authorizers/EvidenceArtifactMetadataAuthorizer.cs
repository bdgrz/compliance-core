using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Evidence.Authorizers;

sealed class EvidenceArtifactMetadataAuthorizer(EvidenceArtifactReadAccess access)
    : IRequestAuthorizer<GetEvidenceArtifactMetadata>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<GetEvidenceArtifactMetadata> context, CancellationToken ct) =>
        access.RequireAsync(context, context.Request.TenantId, context.Request.ArtifactId, ct);
}
