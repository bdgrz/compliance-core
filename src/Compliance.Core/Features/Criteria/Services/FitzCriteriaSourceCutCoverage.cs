using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies an exact resource prefix by streaming its area through a committed cut marker.</summary>
/// <remarks>
/// Uses Fitz's committed area ordering. No child roster is held in memory. A cut is not
/// covered until its physical area position is observed; a quiet tail remains unknown.
/// </remarks>
public sealed class FitzCriteriaSourceCutCoverage(IStreamClient streams) : ICriteriaSourceCutCoverage
{
    const ulong PageSize = 128;
    const ulong PageByteBudget = 512 * 1024;

    /// <inheritdoc />
    public async ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset,
        ulong cutAreaOffset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var areaRoute = $"stream://{stream.Realm}/{stream.Area}/*";
        var targetRoute = stream.ToString();
        ulong nextArea = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await streams.ReadPageAsync(areaRoute, nextArea, PageSize, new StreamFilterSet(),
                PageByteBudget, null, null, ct).ConfigureAwait(false);
            var pageStart = nextArea;
            foreach (var item in page.Items)
            {
                if (item.Kind != StreamReadItemKind.Event || item.Record is not { } record ||
                    record.AreaOffset != nextArea || item.Route != record.Route)
                    return false;
                EventStreamAddress actual;
                try
                { actual = EventStreamAddress.Parse(record.Route); }
                catch (ArgumentException) { return false; }
                catch (FormatException) { return false; }
                if (actual.Realm != stream.Realm || actual.Area != stream.Area)
                    return false;
                if (nextArea == cutAreaOffset)
                    return true;
                if (record.Route == targetRoute && record.Offset >= nextResourceOffset)
                    return false;
                nextArea = checked(nextArea + 1);
            }
            if (!page.Cursor.HasMore || nextArea == pageStart || page.Cursor.LastAreaOffset != nextArea - 1)
                return false;
        }
    }
}
