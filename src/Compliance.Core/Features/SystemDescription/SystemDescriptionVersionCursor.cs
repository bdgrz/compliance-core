using System.Globalization;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

static class SystemDescriptionVersionCursor
{
    public static string Wrap(Uuid tenant, Uuid program, Uuid description, long sequence, int offset) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"v1\n{tenant}\n{program}\n{description}\n{sequence.ToString(CultureInfo.InvariantCulture)}\n{offset.ToString(CultureInfo.InvariantCulture)}"));
    public static bool TryRead(string? cursor, Uuid tenant, Uuid program, Uuid description, out long? sequence, out int offset)
    {
        sequence = null;
        offset = 0;
        if (cursor is null)
            return true;
        if (cursor.Length > 2048)
            return false;
        try
        {
            var values = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor)).Split('\n');
            if (values.Length != 6 || values[0] != "v1" || values[1] != tenant.ToString() || values[2] != program.ToString() || values[3] != description.ToString() ||
                !long.TryParse(values[4], NumberStyles.None, CultureInfo.InvariantCulture, out var cut) || cut < 1 ||
                !int.TryParse(values[5], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 1)
                return false;
            sequence = cut;
            return true;
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
    }
}
