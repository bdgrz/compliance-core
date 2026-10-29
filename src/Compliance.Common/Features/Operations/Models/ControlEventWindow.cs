namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>An event-triggered business window named exactly by its source kind and source id.</summary>
public sealed record ControlEventWindow(string SourceKind, string SourceId) : ControlBusinessWindow
{
    /// <inheritdoc />
    public override bool IsValid => IsToken(SourceKind) && IsToken(SourceId);

    /// <inheritdoc />
    public override string CanonicalKey() => $"event:{SourceKind}:{SourceId}";

    static bool IsToken(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() &&
                                          value.Length <= MaximumSourceLength;
}
