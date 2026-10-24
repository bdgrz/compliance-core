using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Holds one fulfilled evidence request for a risk action completion review. <paramref name="SourceContentEventId" />
///     and <paramref name="SourceRevision" /> name the fulfilment fact the review pinned; the receiver refuses
///     and fences when the request is no longer exactly that. <paramref name="ReceivingOwnerId" /> is the action under
///     review.
/// </summary>
[Discriminator("bdgrz.evidence-request.authorize-action-completion-use", 1)]
public sealed record AuthorizeEvidenceRequestActionCompletionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong SourceRevision, Uuid ReceivingOwnerId,
    Uuid ProgramId)
    : ISourceUseAuthorization, IEvidenceRequestReceiver
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
