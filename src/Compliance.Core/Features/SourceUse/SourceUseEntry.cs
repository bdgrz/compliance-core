using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     One slot of <see cref="SourceUseReservations" />: the grant fact and its receiver generation, and the
///     settlement fact once it exists. A slot with a settlement and no grant is a fence.
/// </summary>
public sealed record SourceUseEntry(ulong ReservationGeneration, DomainEvent? GrantFact, DomainEvent? SettleFact)
{
    /// <summary>True when granted and not settled.</summary>
    public bool IsActive => GrantFact is not null && SettleFact is null;

    /// <summary>True once settled, with or without a grant.</summary>
    public bool IsSettled => SettleFact is not null;

    /// <summary>True when the key was settled before any grant: late acquisition is refused.</summary>
    public bool IsFence => GrantFact is null && SettleFact is not null;
}
