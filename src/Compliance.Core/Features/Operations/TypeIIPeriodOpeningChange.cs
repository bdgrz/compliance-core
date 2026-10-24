namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The complete opening frozen at admission: the exact initial period state, derived from the approved plan and
///     its source chain. The apply step raises it unchanged.
/// </summary>
public sealed record TypeIIPeriodOpeningChange(TypeIIPeriodView State)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "type-ii-period-opening-change.v1";
}
