using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

static class AuditPackageRead
{
    public static async ValueTask<Result<AuditPackageView>> GetAsync(AuditPackageReader reader, Uuid tenantId,
        Uuid programId, Uuid packageId, CancellationToken ct)
    {
        try
        {
            var package = await reader.ReadAsync(tenantId, programId, packageId, ct).ConfigureAwait(false);
            return package.View is { } view && view.TenantId == tenantId && view.ProgramId == programId &&
                   view.AuditPackageId == packageId
                ? Result<AuditPackageView>.Success(view)
                : Result<AuditPackageView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The audit package was not found in this program."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<AuditPackageView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The audit package's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
