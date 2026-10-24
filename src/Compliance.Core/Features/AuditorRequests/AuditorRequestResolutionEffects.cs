using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Deterministic identities and provenance of the request resolution and child materialization processes.</summary>
static class AuditorRequestResolutionEffects
{
    public const string ProcessName = "AuditorRequestResolutionProcess";

    /// <summary>The command id the process driver derives for a receiver step of the resolution root fact.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>The stream key of a response; the resolution process acquires and settles in this canonical order.</summary>
    public static string ResponseKey(Uuid tenantId, Uuid programId, Uuid requestId, Uuid responseId) =>
        AuditorResponse.IdentityOf(tenantId, programId, requestId, responseId).ToString();

    /// <summary>The reservation key of one response use of a resolution operation.</summary>
    public static ReservationKey UseKey(ClosureIntent intent, EventStreamAddress response) => new(
        intent.Header.RootOperationId, intent.Header.FirstOwner, intent.ParentGeneration, response,
        SourceUseReservations.PurposeName(SourceUsePurpose.RequestResolution));

    /// <summary>Provenance of a resolution receiver fact: it copies the root header of the frozen resolution intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);
}
