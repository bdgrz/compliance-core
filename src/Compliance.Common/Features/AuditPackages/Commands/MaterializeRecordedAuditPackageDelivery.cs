using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Commands;

/// <summary>Internal source-scoped effect; the target verifies the committed source and frozen intent.</summary>
[Discriminator("bdgrz.audit-packages.materialize-recorded-audit-package-delivery", 1)]
public sealed record MaterializeRecordedAuditPackageDelivery(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PackageId, Uuid DeliveryId, long ManifestVersion)
    : IRequest<ReceiverOutcome>, IAuditPackageReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
