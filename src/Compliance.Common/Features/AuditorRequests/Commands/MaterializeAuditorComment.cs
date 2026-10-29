using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

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
