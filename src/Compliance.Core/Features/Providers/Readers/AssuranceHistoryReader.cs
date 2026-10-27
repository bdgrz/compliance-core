using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Reads every assurance report and review of one provider from the assurance directory. The read is bracketed by
///     the projection fence, so it fails closed and transient while the directory is behind its source; it is
///     never bounded by a record count, because independent owners carry no tenant-wide history.
/// </summary>
public sealed class AssuranceHistoryReader(IAssuranceReader directory, AssuranceReadConsistency consistency)
{
    const int PageSize = 200;

    /// <summary>Reads the complete report and review history of a provider as of one consistent projection cut.</summary>
    public async ValueTask<Result<ProviderAssuranceRecords>> ReadAsync(Uuid tenantId, Uuid providerId,
        CancellationToken ct)
    {
        var fence = await consistency.CaptureFenceAsync(tenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ProviderAssuranceRecords>.Failure(fence.Error);
        var reports = await ReadAllAsync((cursor, token) =>
            directory.ListReportsAsync(tenantId, providerId, PageSize, cursor, token), ct).ConfigureAwait(false);
        var reviews = await ReadAllAsync((cursor, token) =>
            directory.ListReviewsAsync(tenantId, providerId, PageSize, cursor, token), ct).ConfigureAwait(false);
        var unchanged = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId, fence.Value, ct)
            .ConfigureAwait(false);
        return unchanged.IsSuccess
            ? Result<ProviderAssuranceRecords>.Success(new ProviderAssuranceRecords(reports, reviews))
            : Result<ProviderAssuranceRecords>.Failure(unchanged.Error);
    }

    static async ValueTask<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> page, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var current = await page(cursor, ct).ConfigureAwait(false);
            items.AddRange(current.Items);
            cursor = current.NextCursor;
        } while (cursor is not null);
        return items.AsReadOnly();
    }
}

/// <summary>Every report and review of one provider as of one consistent projection cut.</summary>
public sealed record ProviderAssuranceRecords(IReadOnlyList<AssuranceReportView> Reports,
    IReadOnlyList<ProviderReviewView> Reviews);
