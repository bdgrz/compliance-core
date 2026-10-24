using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The stable identity of a training requirement: the tenant, the program and the normalized identifier. The
///     algorithm is the one the retired catalog published, so every id that was ever published stays valid.
/// </summary>
public static class TrainingRequirementIdentity
{
    /// <summary>The identifier as the identity sees it: trimmed and upper-cased invariantly.</summary>
    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>The deterministic requirement id: a nested UUIDv5 over the tenant, the program and the identifier.</summary>
    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            "training:" + NormalizeIdentifier(identifier));

    /// <summary>True when the normalized identifier has 1 to 80 ASCII letters, digits, hyphens, underscores or periods.</summary>
    public static bool IsValidIdentifier(string normalized) =>
        normalized.Length is >= 1 and <= 80 && normalized.All(static c =>
            char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c is '-' or '_' or '.');
}
