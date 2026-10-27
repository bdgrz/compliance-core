using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.amend-audit-package", 3)]
public sealed record AmendAuditPackage(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PackageId, long ExpectedRevision, string AmendmentReason, IReadOnlyList<Uuid>? IncludedRecordIds = null, AuditPackageContent? Content = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
