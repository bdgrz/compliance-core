using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Applies the exact persisted opening or amendment intent to the period with the calendar permit that holds its
///     range. A cancellation or rejection fence recorded on the period wins over a delayed apply.
/// </summary>
[Discriminator("bdgrz.type-ii-period.apply-period-range", 1)]
public sealed record ApplyTypeIIPeriodRange(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PeriodId)
    : IRequest<ReceiverOutcome>, ITypeIIRangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
