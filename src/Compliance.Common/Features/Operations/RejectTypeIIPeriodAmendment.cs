using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Rejects an exact pending amendment that the calendar proved can never hold its new range. The rejection fences
///     a delayed apply and preserves the current applied range.
/// </summary>
[Discriminator("bdgrz.type-ii-period.reject-amendment", 1)]
public sealed record RejectTypeIIPeriodAmendment(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, ITypeIIRangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
