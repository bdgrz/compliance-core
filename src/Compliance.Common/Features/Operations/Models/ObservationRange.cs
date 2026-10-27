using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     An inclusive Type II calendar date range. Both endpoints belong to the range, so two ranges overlap when each
///     starts on or before the other ends. It is distinct from the exclusive control business window endpoints.
/// </summary>
public sealed record ObservationRange(DateOnly From, DateOnly UntilInclusive)
{
    /// <summary>True when the range is dated and ends on or after its first day.</summary>
    public bool IsValid => From != default && UntilInclusive >= From;

    /// <summary>True when the two inclusive ranges share at least one day.</summary>
    public bool Overlaps(ObservationRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return From <= other.UntilInclusive && UntilInclusive >= other.From;
    }
}
