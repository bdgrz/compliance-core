namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Where one calendar reservation is in its lifecycle.</summary>
public enum TypeIIRangeReservationState
{
    /// <summary>Granted and held until the period applies it; a cancellation or rejection may release it.</summary>
    Pending,

    /// <summary>The live range of its period; only a close or a replacing amendment releases it.</summary>
    Active,

    /// <summary>Released; the range no longer blocks other periods.</summary>
    Released,

    /// <summary>Settled before any grant; a late acquisition of the same operation is refused.</summary>
    Fenced,
}
