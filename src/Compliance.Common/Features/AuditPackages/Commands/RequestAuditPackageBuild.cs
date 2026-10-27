using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.request-audit-package-build", 2)]
public sealed record RequestAuditPackageBuild(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid PackageId, string PackageKind, string BuildKey, long ExpectedManifestRevision, long PackageVersion)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => AuditPackageCommandIdentity.Build(PackageId, PackageVersion, BuildKey);
}
