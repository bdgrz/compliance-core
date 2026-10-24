using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Materializes one admitted comment as its own thread. <paramref name="Source" /> is the
///     <see cref="AuditorCommentAdmitted" /> fact on the request and <paramref name="Intent" /> its frozen
///     <c>ChildAdmissionIntent</c>; the receiver verifies both on the authoritative request.
/// </summary>
[Discriminator("bdgrz.auditor-comment.materialize", 1)]
public sealed record MaterializeAuditorComment(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RequestId,
    Uuid CommentId)
    : IRequest<ReceiverOutcome>, IAuditorRequestReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
