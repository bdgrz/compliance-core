using Cntryl.Fitz;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Honors an optional deployment gate without requiring a local worker.</summary>
static class CriteriaAuthoringReadiness
{
    internal static async ValueTask<bool> CheckAsync(ICriteriaAuthoringReadiness? readiness, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (readiness is null)
            return true;
        try
        {
            var ready = await readiness.IsReadyAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return ready;
        }
        catch (CriteriaSourceUnavailableException) { ct.ThrowIfCancellationRequested(); return false; }
        catch (FitzException) { ct.ThrowIfCancellationRequested(); return false; }
        catch (IOException) { ct.ThrowIfCancellationRequested(); return false; }
        catch (TimeoutException) { ct.ThrowIfCancellationRequested(); return false; }
    }
}
