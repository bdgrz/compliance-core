using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Appends <see cref="FindingClosedV2" /> once every admitted action is held. <paramref name="ActionProofManifest" />
///     is the complete manifest of the held completion facts; the receiver re-derives it from the authoritative actions
///     and refuses any difference. A closure canceled or superseded by a reopened generation is a terminal no-op.
/// </summary>
[Discriminator("bdgrz.finding.complete-closure", 1)]
public sealed record CompleteFindingClosure(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid FindingId,
    ImmutableContentRef ActionProofManifest)
    : IRequest<ReceiverOutcome>, IFindingReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
