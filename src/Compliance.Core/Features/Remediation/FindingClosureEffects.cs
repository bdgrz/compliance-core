using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Deterministic identities and provenance of the finding closure and child materialization processes.</summary>
static class FindingClosureEffects
{
    public const string ProcessName = "FindingClosureProcess";
    public const string MaterializationProcessName = "IndependentChildMaterialization";

    /// <summary>The command id the process driver derives for a receiver step of the closure root fact.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>The stream key of an action; the closure process acquires and settles in this canonical order.</summary>
    public static string ActionKey(Uuid tenantId, Uuid programId, Uuid findingId, Uuid actionId) =>
        CorrectiveAction.IdentityOf(tenantId, programId, findingId, actionId).ToString();

    /// <summary>The reservation key of one action use of a closure operation.</summary>
    public static ReservationKey UseKey(ClosureIntent intent, EventStreamAddress action) => new(
        intent.Header.RootOperationId, intent.Header.FirstOwner, intent.ParentGeneration, action,
        SourceUseReservations.PurposeName(SourceUsePurpose.FindingClosure));

    /// <summary>Provenance of a closure receiver fact: it copies the root header of the frozen closure intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);
}
