using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic identities and provenance of the firm staff registration process.</summary>
static class FirmStaffEffects
{
    public const string ProcessName = "FirmStaffRegistrationProcess";

    /// <summary>The only generation of a registration; a rejected registration is never retried.</summary>
    public const long Generation = 1;

    /// <summary>The command id the process driver derives for a receiver step of the root registration.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen registration intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        FirmStaffRegistrationIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) => new(commandId,
        intent.Header.RootOperationId, causationEventId, commandKind, intent.Header.OriginalActor, owner,
        intent.Header.Episode, intent.Header.OriginalExpectedRevision, intent.Header.AuthoredIntentSha256, acceptedAt,
        owner, owner, intent.Header.FirstOwner);
}
