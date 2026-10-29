using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

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
