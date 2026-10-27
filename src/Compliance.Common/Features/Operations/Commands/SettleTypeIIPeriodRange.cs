using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Settles exactly one reservation named by a period fact: an applied range becomes live and releases the range it
///     replaced, a close-frozen period releases its exact live range, and a cancellation or rejection releases only
///     the pending range. It never releases a newer reservation, and missing period state is transient.
/// </summary>
[Discriminator("bdgrz.type-ii-observation-calendar.settle-period-range", 1)]
public sealed record SettleTypeIIPeriodRange(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PeriodId)
    : IRequest<ReceiverOutcome>, ITypeIIRangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
