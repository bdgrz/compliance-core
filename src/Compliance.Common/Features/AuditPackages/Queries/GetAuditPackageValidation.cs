using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPackages.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.audit-packages.get-audit-package-validation", 1)]
public sealed record GetAuditPackageValidation(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageValidationId)
    : IRequest<AuditPackageValidationView>, IProgramReadRequest, ICallable;
