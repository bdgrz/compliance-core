using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     Stable identity of one source use: the root operation, its first owner, the frozen source authorization
///     generation, the target owner and the purpose. Every effect and the terminal settlement of the operation use the
///     same key. A root operation has exactly one first owner and a target hears each purpose once, so owners slot a use
///     by root operation, generation and purpose.
/// </summary>
public sealed record ReservationKey(Uuid RootOperationId, EventStreamAddress FirstOwner, ulong SourceGeneration,
    EventStreamAddress Target, string Purpose);
