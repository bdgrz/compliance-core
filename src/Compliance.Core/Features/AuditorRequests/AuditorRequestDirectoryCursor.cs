using System.Globalization;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Preserves the underlying opaque directory cursor while binding its authorized tenant/program/page scope.</summary>
static class AuditorRequestDirectoryCursor
{
    public static string? Wrap(Uuid tenant, Uuid program, int limit, string? cursor) => cursor is null ? null
        : Convert.ToBase64String(Encoding.UTF8.GetBytes(Scope(tenant, program, limit) + cursor));

    public static bool TryUnwrap(Uuid tenant, Uuid program, int limit, string? cursor, out string? inner)
    {
        inner = null;
        if (cursor is null)
            return true;
        if (cursor.Length > 16384)
            return false;
        try
        {
            var value = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor));
            var scope = Scope(tenant, program, limit);
            if (!value.StartsWith(scope, StringComparison.Ordinal) || value.Length == scope.Length)
                return false;
            inner = value[scope.Length..];
            return true;
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
    }

    static string Scope(Uuid tenant, Uuid program, int limit) =>
        $"auditor_requests_v1\n{tenant}\n{program}\n{limit.ToString(CultureInfo.InvariantCulture)}\n";
}
