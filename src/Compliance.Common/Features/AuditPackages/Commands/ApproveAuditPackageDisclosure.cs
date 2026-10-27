using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.approve-audit-package-disclosure", 2)]
public sealed record ApproveAuditPackageDisclosure(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, long ExpectedRevision, string Recipient, string Purpose, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
