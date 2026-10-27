using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed class GetAuditPackageHandler(AuditPackageReader packages) : IRequestHandler<GetAuditPackage, AuditPackageView>
{
    public ValueTask<Result<AuditPackageView>> HandleAsync(IRequestContext<GetAuditPackage> context, CancellationToken ct)
    {
        var request = context.Request;
        return AuditPackageRead.GetAsync(packages, request.TenantId, request.ProgramId, request.AuditPackageId, ct);
    }
}
