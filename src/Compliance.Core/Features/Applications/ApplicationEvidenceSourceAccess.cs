using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplicationEvidenceSourceAccess(EvidenceArtifactReadAccess access)
    : IApplicationEvidenceSourceAccess
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid userId, Uuid artifactId,
        CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        var result = await access.RequireAsync(tenantId, userId, artifactId, ct)
            .ConfigureAwait(false);
        return result.IsSuccess;
    }
}
