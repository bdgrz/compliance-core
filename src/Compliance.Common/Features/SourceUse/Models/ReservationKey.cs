using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse.Models;

/// <summary>
///     Stable identity of one source use: the root operation, its first owner, the frozen source authorization
///     generation, the target owner and the purpose. Every effect and the terminal settlement of the operation use the
///     same key. Admission is scoped to the first owner, so different first owners may accept the same root operation
///     identity. Every reservation slot includes that first owner, generation and purpose.
/// </summary>
public sealed record ReservationKey(Uuid RootOperationId, EventStreamAddress FirstOwner, ulong SourceGeneration,
    EventStreamAddress Target, string Purpose);
