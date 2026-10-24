using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic identities and provenance of the client independence preview process.</summary>
static class ClientIndependencePreviewEffects
{
    public const string ProcessName = "ClientIndependencePreviewProcess";

    /// <summary>The only generation of a preview; an admitted evaluation is never re-materialized.</summary>
    public const long Generation = 1;

    /// <summary>The command id the process driver derives for a receiver step of the root admission.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen preview intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        ClientIndependencePreviewIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);
}
