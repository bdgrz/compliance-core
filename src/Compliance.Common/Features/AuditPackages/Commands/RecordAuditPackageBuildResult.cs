using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Successful-only callback rooted in the original committed build admission, never an attempt.</summary>
[Discriminator("bdgrz.audit-packages.record-audit-package-build-result", 2)]
public sealed record RecordAuditPackageBuildResult(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid PackageId, Uuid BuildId, AuditPackageBuildOutput Output)
    : IRequest<ReceiverOutcome>, IAuditPackageReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
