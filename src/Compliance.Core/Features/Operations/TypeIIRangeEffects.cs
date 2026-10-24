using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Deterministic identities and provenance of the Type II range process.</summary>
static class TypeIIRangeEffects
{
    public const string ProcessName = "TypeIIRangeProcess";

    /// <summary>The wire name of the purpose of every Type II range reservation.</summary>
    public const string Purpose = "type_ii_observation_range";

    /// <summary>The only source generation of an opening or amendment; a rejected one is never retried.</summary>
    public const long Generation = 1;

    /// <summary>The command id the process driver derives for a receiver step of the root opening or amendment.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", "-", episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen range intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        TypeIIRangeIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);

    /// <summary>The reservation key of the new range of an operation of one period on one calendar.</summary>
    public static ReservationKey KeyOf(Uuid operationId, EventStreamAddress period, EventStreamAddress calendar) =>
        new(operationId, period, Generation, calendar, Purpose);
}
