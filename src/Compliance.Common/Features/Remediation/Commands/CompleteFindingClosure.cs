using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

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
