using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Authored package intent whose exact first-owner acceptance is replayed on every identical retry.</summary>
[Discriminator("bdgrz.audit-packages.record-audit-package-delivery", 2)]
public sealed record RecordAuditPackageDelivery(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, string Recipient, DateTimeOffset DeliveredAt, string DeliveryReference, Uuid DeliveryId, long ExpectedManifestRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => DeliveryId;
}
