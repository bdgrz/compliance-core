using System.Globalization;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Scopes an opaque directory continuation to an authorized query and source cut.</summary>
static class RuntimeCriteriaPageCursor
{
    internal readonly record struct Value(string Kind, Uuid TenantId, Uuid EditionId,
        Uuid PublicationEventId, string Generation, string Category, string EntryKind,
        string ParentIdentifier, long SnapshotOrdinal, string Inner);

    internal static bool IsCurrent(Value value, string epoch, long revision) =>
        value.Generation == epoch && value.SnapshotOrdinal == revision;

    internal static string Encode(Value value)
    {
        var fields = new[]
        {
            value.Kind, value.TenantId.ToString(), value.EditionId.ToString(),
            value.PublicationEventId.ToString(), value.Generation, value.Category,
            value.EntryKind, value.ParentIdentifier,
            value.SnapshotOrdinal.ToString(CultureInfo.InvariantCulture), value.Inner,
        };
        return string.Join('.', fields.Select(EncodeField));
    }

    internal static bool TryDecode(string? token, out Value value)
    {
        value = default;
        if (string.IsNullOrEmpty(token) || token.Length > 16_384)
            return false;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 10)
                return false;
            var values = parts.Select(DecodeField).ToArray();
            if (!Uuid.TryParse(values[1], null, out var tenant) || tenant == Uuid.Empty ||
                !Uuid.TryParse(values[2], null, out var edition) ||
                !Uuid.TryParse(values[3], null, out var publication) ||
                !long.TryParse(values[8], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) ||
                ordinal < 0 || string.IsNullOrEmpty(values[0]) || string.IsNullOrEmpty(values[4]) ||
                string.IsNullOrEmpty(values[9]))
                return false;
            value = new Value(values[0], tenant, edition, publication, values[4], values[5],
                values[6], values[7], ordinal, values[9]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    static string EncodeField(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string DecodeField(string value)
    {
        var encoded = value.Replace('-', '+').Replace('_', '/');
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '=')));
    }
}
