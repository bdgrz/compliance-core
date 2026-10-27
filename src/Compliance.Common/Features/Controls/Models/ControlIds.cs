using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>The existing identifier claim identity; a caller cannot choose a second ID for the same identifier.</summary>
public static class ControlIds
{
    public static string NormalizeIdentifier(string? identifier) => identifier?.Trim().ToUpperInvariant() ?? string.Empty;
    public static Uuid For(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()), NormalizeIdentifier(identifier));
}
