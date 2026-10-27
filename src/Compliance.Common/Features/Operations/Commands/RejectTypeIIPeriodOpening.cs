using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>
///     Rejects an exact pending opening that the calendar proved can never hold its range. The rejection fences a
///     delayed apply; an opening that already applied recovers forward and is not rejected.
/// </summary>
[Discriminator("bdgrz.type-ii-period.reject-opening", 1)]
public sealed record RejectTypeIIPeriodOpening(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, ITypeIIRangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
