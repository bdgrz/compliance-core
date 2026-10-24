using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Deterministic identities, generations and provenance of the access review launch and completion processes.</summary>
static class AccessReviewEffects
{
    /// <summary>The unordered launch fan-out that materializes every item of a launched campaign.</summary>
    public const string LaunchFanOutName = "AccessReviewLaunchFanOut";

    /// <summary>The ordered process that holds every item, closes the campaign and settles the holds.</summary>
    public const string CompletionProcessName = "AccessReviewCampaignCompletionProcess";

    /// <summary>The only episode of a campaign and of each of its items.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>The only source generation of a launch or completion; a canceled completion is admitted again as a new operation.</summary>
    public const long Generation = 1;

    /// <summary>The identity of an item: derived from the tenant, campaign and item ids so a mismatched pair is simply absent.</summary>
    public static Uuid ItemStreamId(Uuid tenantId, Uuid campaignId, Uuid itemId) =>
        AggregateIdentity.Create(nameof(AccessReviewItem), tenantId, campaignId, itemId);

    /// <summary>The stream of one item.</summary>
    public static EventStreamAddress ItemStream(Uuid tenantId, Uuid campaignId, Uuid itemId) =>
        new(tenantId.ToString(), AccessReviewItem.Area, ItemStreamId(tenantId, campaignId, itemId).ToString());

    /// <summary>The stream of one campaign.</summary>
    public static EventStreamAddress CampaignStream(Uuid tenantId, Uuid campaignId) =>
        new(tenantId.ToString(), AccessReviewCampaign.Area, campaignId.ToString());

    /// <summary>The reviewer responsibility episode an item opens with.</summary>
    public static Uuid ReviewerEpisode(Uuid campaignId, Uuid itemId) =>
        Uuid.CreateVersion5(campaignId, $"reviewer-episode:{itemId}:0");

    /// <summary>The command id the process driver derives for the receiver of one target of a launch or completion.</summary>
    public static Uuid ReceiverCommandId(string processName, Uuid rootSourceEventId, string receiver, string? target,
        string episode) => EffectIdentity.CommandId(rootSourceEventId, $"{processName}:{receiver}", target ?? "-",
        episode);

    /// <summary>The provenance of a root operation on its own owner stream.</summary>
    public static OperationProvenance Root(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, long? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) => new(logicalId,
        logicalId, null, commandKind, actor, owner, InitialEpisode, expectedRevision is { } expected ? (ulong)expected : null,
        intentDigest, acceptedAt, owner, owner);
}
