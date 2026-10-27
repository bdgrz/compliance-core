using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Internal source-scoped effect; the target verifies the committed source and frozen intent.</summary>
[Discriminator("bdgrz.audit-packages.start-audit-package-build", 1)]
public sealed record StartAuditPackageBuild(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PackageId, Uuid BuildId, long ManifestVersion)
    : IRequest<ReceiverOutcome>, IAuditPackageReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
