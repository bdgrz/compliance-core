using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Materializes one admitted sample selection on its own owner, after the request closed if need be, without
///     reopening the request. <paramref name="Source" /> is the <see cref="AuditorSampleSelectionAdmitted" /> fact and
///     <paramref name="Intent" /> its frozen <see cref="AuditorSampleSelectionIntent" />.
/// </summary>
[Discriminator("bdgrz.auditor-sample-selection.materialize", 1)]
public sealed record MaterializeAuditorSampleSelection(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid RequestId, Uuid SelectionId)
    : IRequest<ReceiverOutcome>, IAuditorRequestReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
