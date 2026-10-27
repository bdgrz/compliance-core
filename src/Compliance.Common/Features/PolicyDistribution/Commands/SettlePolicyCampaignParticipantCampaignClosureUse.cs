using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Settles one participant's closure use after the campaign's terminal fact. <paramref name="Source" /> is the
///     terminal <see cref="PolicyCampaignClosed" /> or <see cref="PolicyCampaignClosureCanceled" /> fact; the
///     disposition must match its kind. A settlement before any grant fences the key against late acquisition.
/// </summary>
[Discriminator("bdgrz.policy-campaign-participant.settle-campaign-closure-use", 1)]
public sealed record SettlePolicyCampaignParticipantCampaignClosureUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ReceivingOwnerId, Uuid PersonId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IPolicyCampaignReceiver
{
    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid CampaignId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
