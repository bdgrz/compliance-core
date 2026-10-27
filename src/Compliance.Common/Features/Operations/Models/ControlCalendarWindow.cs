using System.Globalization;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A calendar business window: <paramref name="From" /> inclusive to <paramref name="UntilExclusive" /> exclusive.</summary>
public sealed record ControlCalendarWindow(DateOnly From, DateOnly UntilExclusive) : ControlBusinessWindow
{
    /// <inheritdoc />
    public override bool IsValid => From < UntilExclusive;

    /// <inheritdoc />
    public override string CanonicalKey() => string.Create(CultureInfo.InvariantCulture,
        $"calendar:{From:yyyy-MM-dd}:{UntilExclusive:yyyy-MM-dd}");
}
