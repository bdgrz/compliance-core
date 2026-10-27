using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.approve-audit-package-validation-override", 2)]
public sealed record ApproveAuditPackageValidationOverride(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, long ExpectedRevision, string ValidationIssue, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
