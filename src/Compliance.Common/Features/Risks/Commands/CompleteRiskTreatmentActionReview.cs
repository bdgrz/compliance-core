using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Appends <see cref="RiskTreatmentActionCompletionReviewed" /> once every cited evidence request is held.
///     <paramref name="EvidenceUseProofs" /> is the complete manifest of the held evidence fulfilments; the receiver
///     re-derives it from the authoritative evidence requests and refuses any difference. A review canceled or
///     superseded is a terminal no-op.
/// </summary>
[Discriminator("bdgrz.risk-treatment-action.complete-review", 1)]
public sealed record CompleteRiskTreatmentActionReview(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid ActionId, ImmutableContentRef EvidenceUseProofs)
    : IRequest<ReceiverOutcome>, IRiskActionReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
