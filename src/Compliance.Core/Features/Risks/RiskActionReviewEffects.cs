using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Deterministic identities and provenance of the risk action completion review process.</summary>
static class RiskActionReviewEffects
{
    public const string ProcessName = "RiskActionCompletionReviewProcess";

    /// <summary>The command id the process driver derives for a receiver step of the review root fact.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>The stream key of an evidence request; the review acquires and settles in this canonical order.</summary>
    public static string EvidenceKey(Uuid tenantId, Uuid programId, Uuid evidenceRequestId) =>
        EvidenceRequest.IdentityOf(tenantId, programId, evidenceRequestId).ToString();

    /// <summary>The reservation key of one evidence request use of a review operation.</summary>
    public static ReservationKey UseKey(RiskActionCompletionReviewIntent intent, long generation,
        EventStreamAddress evidenceRequest) => new(intent.Header.RootOperationId, intent.Header.FirstOwner,
        (ulong)generation, evidenceRequest, SourceUseReservations.PurposeName(SourceUsePurpose.ActionCompletionReview));

    /// <summary>Provenance of a review receiver fact: it copies the root header of the frozen review intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);
}
