using System.Globalization;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Opaque native keyset cursor scoped to the full Portia projection identity.</summary>
static class MethodologyTemplateDirectoryCursor
{
    const string Format = "cassie-methodology-directory.v1";

    public static string Encode(string projectionKey, long ticks, Uuid templateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectionKey);
        if (ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks || templateId == Uuid.Empty)
            throw new ArgumentException("A directory cursor requires its exact published ordering key.");
        var scope = Convert.ToBase64String(Encoding.UTF8.GetBytes(projectionKey));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Format}\n{scope}\n{ticks.ToString(CultureInfo.InvariantCulture)}\n{templateId}"));
    }

    public static bool TryDecode(string projectionKey, string? cursor, out long ticks, out Uuid templateId)
    {
        ticks = 0;
        templateId = Uuid.Empty;
        if (cursor is null || cursor.Length > 8192)
            return false;
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            var parts = utf8.GetString(Convert.FromBase64String(cursor)).Split('\n');
            return parts.Length == 4 && parts[0] == Format &&
                utf8.GetString(Convert.FromBase64String(parts[1])) == projectionKey &&
                long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) &&
                ticks <= DateTimeOffset.MaxValue.UtcTicks &&
                Uuid.TryParse(parts[3], CultureInfo.InvariantCulture, out templateId) && templateId != Uuid.Empty &&
                parts[3] == templateId.ToString();
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
    }
}
