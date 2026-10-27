using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>
///     Holds one exact gap plan for a readiness decision. <paramref name="SourceContentEventId" /> and
///     <paramref name="SourceRevision" /> are the plan fact and plan revision the decision froze at admission;
///     an absent plan is the empty event at revision zero. The receiver refuses and fences when the gap plan is no
///     longer exactly that, so a changed plan fences the decision instead of being silently adopted.
///     <paramref name="ReceivingOwnerId" /> is the program the decision belongs to.
/// </summary>
[Discriminator("bdgrz.readiness-gap-plan.authorize-readiness-decision-use", 1)]
public sealed record AuthorizeReadinessGapPlanReadinessDecisionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong SourceRevision, Uuid ReceivingOwnerId)
    : ISourceUseAuthorization, IReadinessReceiver
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
