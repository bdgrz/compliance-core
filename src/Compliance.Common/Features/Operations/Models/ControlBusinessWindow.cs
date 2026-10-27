using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     The business performance window of one control occurrence: a calendar window <c>[From, UntilExclusive)</c> or
///     an event source. A generator, plan version, period or trigger text is never part of a window, so every
///     generator that selects the same window names the same occurrence.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ControlCalendarWindow), "calendar")]
[JsonDerivedType(typeof(ControlEventWindow), "event")]
public abstract record ControlBusinessWindow
{
    /// <summary>Longest source kind or source id of an event window.</summary>
    public const int MaximumSourceLength = 200;

    private protected ControlBusinessWindow()
    {
    }

    /// <summary>True when the window satisfies its shape rules.</summary>
    [JsonIgnore]
    public abstract bool IsValid { get; }

    /// <summary>The canonical text that enters the occurrence identity; equal windows have equal keys.</summary>
    public abstract string CanonicalKey();
}
