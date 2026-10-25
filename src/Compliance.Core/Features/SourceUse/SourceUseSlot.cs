using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

readonly record struct SourceUseSlot(Uuid Root, EventStreamAddress FirstOwner, ulong SourceGeneration, string Purpose)
{
    public static SourceUseSlot Of(EventStreamAddress owner, ReservationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Target != owner)
            throw new ArgumentException("The key names another target owner.", nameof(key));
        return new(key.RootOperationId, key.FirstOwner, key.SourceGeneration, key.Purpose);
    }

    public static SourceUseSlot Of(EventStreamAddress owner, SourceUsePurpose purpose, ISourceUseFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return Of(owner, purpose, fact.Operation, fact.Generation, fact.Owner);
    }

    public static SourceUseSlot Of(EventStreamAddress owner, SourceUsePurpose purpose, OperationProvenance operation,
        long generation, EventStreamAddress? factOwner = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (factOwner is not null && factOwner != owner || operation.Owner != owner || generation < 0 ||
            operation.RootOperationId == Uuid.Empty)
            throw new InvalidOperationException("A source use fact belongs to its own owner stream and root operation.");
        return new(operation.RootOperationId, operation.FirstOwner ?? operation.Owner, (ulong)generation,
            SourceUseReservations.PurposeName(purpose));
    }
}
