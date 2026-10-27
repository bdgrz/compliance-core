using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Events;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Materializes one admitted corrective action on its own owner at revision zero. <paramref name="Source" /> is the
///     <see cref="CorrectiveActionAdmitted" /> fact on the finding, <paramref name="Intent" /> its frozen
///     <c>ChildAdmissionIntent</c> and <paramref name="Generation" /> the finding generation at admission. The receiver
///     verifies the source on the authoritative finding before it appends anything.
/// </summary>
[Discriminator("bdgrz.corrective-action.materialize", 1)]
public sealed record MaterializeCorrectiveAction(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid FindingId, Uuid ActionId)
    : IRequest<ReceiverOutcome>, IFindingReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
