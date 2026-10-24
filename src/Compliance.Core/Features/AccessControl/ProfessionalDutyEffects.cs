using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic identities and provenance of the professional-duty change process.</summary>
static class ProfessionalDutyEffects
{
    public const string ProcessName = "ProfessionalDutyChangeProcess";

    /// <summary>The only generation of a change; a rejected change is never retried under the same assertion.</summary>
    public const long Generation = 1;

    /// <summary>The command id the process driver derives for a receiver step of the root change.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string? target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target ?? "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen change intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        ProfessionalDutyChangeIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);
}
