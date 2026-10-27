using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Computes the historical preview from the frozen intent of an admitted evaluation and records it on the
///     decision owner of that evaluation. It reads only the frozen snapshot, never a live source, and the preview
///     always stays blocked from production acceptance.
/// </summary>
[Discriminator("bdgrz.client-independence-decision.materialize-preview", 1)]
public sealed record MaterializeClientIndependencePreview(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid EvaluationId)
    : IRequest<ReceiverOutcome>, IClientIndependencePreviewReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
