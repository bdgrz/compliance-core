using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Services;

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
