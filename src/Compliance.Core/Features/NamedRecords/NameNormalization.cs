namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     Version one of the governed-name normalization: surrounding whitespace is trimmed and the name is upper-cased
///     with the invariant culture, which is the exact equality the registers always enforced
///     (<c>StringComparer.OrdinalIgnoreCase</c> over the trimmed name) in a form that can seed an identity.
/// </summary>
public static class NameNormalization
{
    /// <summary>The normalized form of a governed name.</summary>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }
}
