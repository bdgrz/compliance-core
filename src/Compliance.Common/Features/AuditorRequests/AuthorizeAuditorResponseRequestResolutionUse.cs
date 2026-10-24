using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Holds one response with an accepted external disposition for a request resolution.
///     <paramref name="SourceContentEventId" /> and <paramref name="SourceRevision" /> name the response fact the
///     process pinned; the receiver refuses and fences when the response is no longer exactly that or has no accepted
///     disposition for its delivered version. A response that is not materialized yet is a transient wait.
/// </summary>
[Discriminator("bdgrz.auditor-response.authorize-request-resolution-use", 1)]
public sealed record AuthorizeAuditorResponseRequestResolutionUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong SourceRevision, Uuid ReceivingOwnerId,
    Uuid ProgramId)
    : ISourceUseAuthorization, IAuditorRequestReceiver
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
