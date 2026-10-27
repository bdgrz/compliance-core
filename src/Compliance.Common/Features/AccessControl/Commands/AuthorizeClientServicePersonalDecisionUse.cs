using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Acquires the exact complete service history and epoch a personal decision relies on for one personal engagement decision. <see cref="Source" /> is the accepted admission fact;
///     the receiver verifies it by hydrating the engagement owner and never trusts a caller-supplied policy value.
/// </summary>
[Discriminator("bdgrz.client-service-independence.authorize-client-service-personal-decision-use", 1)]
public sealed record AuthorizeClientServicePersonalDecisionUse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid SourceOwnerId,
    Uuid SourceContentEventId, long ServiceEpoch, Uuid EngagementId)
    : ISourceUseAuthorization, IEngagementPersonalDecisionUse, IEngagementPersonalDecisionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    /// <summary>The pinned nonnegative epoch in the shared source-use protocol.</summary>
    [JsonIgnore]
    public ulong ExpectedContentRevision => ServiceEpoch >= 0 ? (ulong)ServiceEpoch : ulong.MaxValue;
}
