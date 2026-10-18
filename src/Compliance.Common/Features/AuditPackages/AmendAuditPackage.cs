using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records a complete desired frozen source selection for the next package version.</summary>
[Discriminator("bdgrz.audit-packages.amend-audit-package", 2)]
public sealed record AmendAuditPackage(Uuid TenantId, Uuid ProgramId, Uuid PackageId,
    long ExpectedRevision, string AmendmentReason, IReadOnlyList<Uuid>? IncludedRecordIds = null,
    AuditPackageContent? Content = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
