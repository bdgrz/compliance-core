using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPackages.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-packages.get-audit-package-manifest", 1)]
public sealed record GetAuditPackageManifest(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageManifestId)
    : IRequest<AuditPackageManifestView>, IProgramReadRequest, ICallable;
