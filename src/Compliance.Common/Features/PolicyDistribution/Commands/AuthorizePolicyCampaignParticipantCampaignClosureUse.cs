using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Holds the exact applied results of one participant for the campaign closure. <paramref name="Source" /> is the
///     accepted <see cref="PolicyCampaignClosureAccepted" /> fact; the receiver verifies it by hydrating the campaign.
///     <paramref name="SourceOwnerId" /> is the participant owner, <paramref name="SourceRevision" /> the
///     number of admitted results the participant must already have applied, and
///     <paramref name="SourceContentEventId" /> the admission fact of the participant's last admitted result.
/// </summary>
[Discriminator("bdgrz.policy-campaign-participant.authorize-campaign-closure-use", 1)]
public sealed record AuthorizePolicyCampaignParticipantCampaignClosureUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, long SourceRevision, Uuid ReceivingOwnerId, Uuid PersonId)
    : ISourceUseAuthorization, IPolicyCampaignReceiver
{
    /// <summary>The source revision in the shared use protocol; invalid negative input cannot match a held revision.</summary>
    [JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision >= 0 ? (ulong)SourceRevision : ulong.MaxValue;

    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid CampaignId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
