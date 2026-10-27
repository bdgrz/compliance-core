using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Materializes one admitted response draft on its own owner. <paramref name="Source" /> is the
///     <see cref="AuditorResponseAdmitted" /> fact on the request, <paramref name="Intent" /> its frozen
///     <c>ChildAdmissionIntent</c> and <paramref name="Generation" /> the request generation at admission. The receiver
///     verifies the source on the authoritative request before it appends anything.
/// </summary>
[Discriminator("bdgrz.auditor-response.materialize", 1)]
public sealed record MaterializeAuditorResponse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RequestId,
    Uuid ResponseId)
    : IRequest<ReceiverOutcome>, IAuditorRequestReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
