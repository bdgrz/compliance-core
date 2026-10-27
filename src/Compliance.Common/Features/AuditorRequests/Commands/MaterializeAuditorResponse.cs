using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
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
