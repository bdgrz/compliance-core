using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

static class AccessControlDirectoryCursor
{
    static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Scope(string projection, string directory, string parent, string? search, bool descending)
    {
        var values = new[] { projection, directory, parent, search, descending ? "descending" : "ascending" };
        var key = string.Concat(values.Select(static value => value is null ? "n;" :
            value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value));
        return Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(key)));
    }

    public static string Encode(string scope, string sort, string id) =>
        $"1.{scope}.{Convert.ToBase64String(Utf8.GetBytes(sort))}.{Convert.ToBase64String(Utf8.GetBytes(id))}";

    public static (string Sort, string Id)? Decode(string? cursor, string scope)
    {
        if (cursor is null)
            return null;
        if (cursor.Length > 8192)
            throw new AccessControlDirectoryQueryException();
        var parts = cursor.Split('.');
        if (parts.Length != 4 || parts[0] != "1" || parts[1] != scope)
            throw new AccessControlDirectoryQueryException();
        try
        {
            return (Utf8.GetString(Convert.FromBase64String(parts[2])), Utf8.GetString(Convert.FromBase64String(parts[3])));
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw new AccessControlDirectoryQueryException();
        }
    }
}
