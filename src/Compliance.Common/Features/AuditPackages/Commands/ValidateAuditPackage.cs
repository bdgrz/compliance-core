using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.validate-audit-package", 2)]
public sealed record ValidateAuditPackage(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, long ExpectedRevision, string ValidationKey)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
