using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Creates a package from a frozen examination snapshot and explicitly selected source versions.</summary>
[Discriminator("bdgrz.audit-packages.create-audit-package", 1)]
public sealed record CreateAuditPackage(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid PackageId,
    AuditPackageContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
