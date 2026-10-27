using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.create-audit-package", 2)]
public sealed record CreateAuditPackage(Uuid TenantId, Uuid ProgramId, Uuid PackageId, AuditPackageContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => PackageId;
}
