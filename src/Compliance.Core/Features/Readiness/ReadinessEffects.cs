using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Deterministic identities and provenance of the readiness run and readiness decision processes.</summary>
static class ReadinessEffects
{
    public const string RunProcessName = "ReadinessRunProcess";
    public const string DecisionProcessName = "ReadinessDecisionProcess";

    /// <summary>The command id the decision process driver derives for a receiver step of the decision root fact.</summary>
    public static Uuid DecisionReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{DecisionProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>The stream key of a gap plan; the decision process acquires and settles in this canonical order.</summary>
    public static string GapKey(Uuid tenantId, Uuid programId, Uuid gapId) =>
        ReadinessGapPlan.IdentityOf(tenantId, programId, gapId).ToString();

    /// <summary>The reservation key of one gap plan use of a decision operation.</summary>
    public static ReservationKey UseKey(ReadinessDecisionIntent intent, long generation, EventStreamAddress plan) => new(
        intent.Header.RootOperationId, intent.Header.FirstOwner, (ulong)generation, plan,
        SourceUseReservations.PurposeName(SourceUsePurpose.ReadinessDecision));

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);
}
