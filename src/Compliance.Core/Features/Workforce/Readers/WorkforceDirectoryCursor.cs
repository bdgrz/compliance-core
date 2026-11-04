using System.Security.Cryptography;
using System.Text;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

static class WorkforceDirectoryCursor
{
    static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Scope(string projectionKey, string table, string query) =>
        Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(
            $"{projectionKey.Length}:{projectionKey}{table.Length}:{table}{query.Length}:{query}")));

    public static string Encode(string scope, string sort, string id) =>
        $"1.{scope}.{Convert.ToBase64String(Utf8.GetBytes(sort))}.{id}";

    public static (string Sort, string Id)? Decode(string? cursor, string scope)
    {
        if (cursor is null)
            return null;
        if (cursor.Length > 8192)
            throw new WorkforceDirectoryQueryException();
        var parts = cursor.Split('.');
        if (parts.Length != 4 || parts[0] != "1" || parts[1] != scope ||
            !Guid.TryParseExact(parts[3], "D", out _))
            throw new WorkforceDirectoryQueryException();
        try
        {
            return (Utf8.GetString(Convert.FromBase64String(parts[2])), parts[3]);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw new WorkforceDirectoryQueryException();
        }
    }
}
