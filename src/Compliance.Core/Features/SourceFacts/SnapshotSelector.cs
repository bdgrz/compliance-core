namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// A global area, or one resource inside an area, scanned across every realm. The reader keeps only the requested
/// realm's records; the selector itself never names a realm because Fitz continuation tokens are global-only.
/// </summary>
public sealed record SnapshotSelector(string Area, string? Resource = null)
{
    /// <summary>The global route sent to the broker for this selector.</summary>
    public string Route
    {
        get
        {
            Validate(Area, nameof(Area));
            if (Resource is not null)
                Validate(Resource, nameof(Resource));
            return $"*/{Area}/{Resource ?? "*"}";
        }
    }

    static void Validate(string segment, string name)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains('/', StringComparison.Ordinal) ||
            segment.Contains('*', StringComparison.Ordinal))
            throw new ArgumentException("A selector segment must be a concrete name.", name);
    }
}
