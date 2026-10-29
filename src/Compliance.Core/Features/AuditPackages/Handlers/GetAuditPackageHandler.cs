using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Queries;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
namespace Bdgrz.Compliance.Features.AuditPackages.Handlers;

public sealed class GetAuditPackageHandler(AuditPackageReader packages) : IRequestHandler<GetAuditPackage, AuditPackageView>
{
    public ValueTask<Result<AuditPackageView>> HandleAsync(IRequestContext<GetAuditPackage> context, CancellationToken ct)
    {
        var request = context.Request;
        return AuditPackageRead.GetAsync(packages, request.TenantId, request.ProgramId, request.AuditPackageId, ct);
    }
}
