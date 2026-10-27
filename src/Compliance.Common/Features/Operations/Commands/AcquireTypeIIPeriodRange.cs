using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Holds the exact new range, and for an amendment the old range as well, on the program calendar. The calendar
///     refuses a range that overlaps another period's live range; the accepted opening or amendment is then
///     rejected. <paramref name="OperationId" /> and <paramref name="Generation" /> bind the reservation.
/// </summary>
[Discriminator("bdgrz.type-ii-observation-calendar.acquire-period-range", 1)]
public sealed record AcquireTypeIIPeriodRange(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid PeriodId,
    ObservationRange? OldRange, ObservationRange NewRange)
    : IRequest<ReceiverOutcome>, ITypeIIRangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
