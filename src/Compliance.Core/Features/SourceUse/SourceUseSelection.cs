using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>Explicit request-local history selection made before aggregate replay.</summary>
public static class SourceUseSelection
{
    /// <summary>Selects the exact historical reservation keys needed by this reader or receiver.</summary>
    public static TOwner ForSourceUses<TOwner>(this TOwner owner, params ReservationKey[] keys)
        where TOwner : Aggregate, ISourceUseOwner
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
            owner.Uses.Select(key);
        return owner;
    }
    /// <summary>Selects one exact key derived from the unhydrated owner's immutable stream identity.</summary>
    public static TOwner ForSourceUse<TOwner>(this TOwner owner, Func<TOwner, ReservationKey> select)
        where TOwner : Aggregate, ISourceUseOwner
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(select);
        return owner.ForSourceUses(select(owner));
    }
}
