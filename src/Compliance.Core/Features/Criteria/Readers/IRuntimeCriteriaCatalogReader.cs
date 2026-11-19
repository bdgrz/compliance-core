using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

/// <summary>Read-side discovery as of the projection checkpoint; these results do not authorize writes.</summary>
public interface IRuntimeCriteriaCatalogReader
{
    /// <summary>Reads the legacy unpaged directory when it fits in one bounded projection snapshot.</summary>
    ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> ListLegacyEditionsAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<IReadOnlyList<CriteriaCatalogEdition>>.Failure(
            new RequestError(RequestErrorKind.Conflict,
                "The edition directory has not been read from one qualified snapshot.", true)));
    }

    /// <summary>Reads the legacy edition response from one projection snapshot.</summary>
    /// <remarks>Implementations that cannot obtain an atomic, bounded response must leave it pending.</remarks>
    async ValueTask<Result<CriteriaCatalogEdition>> GetLegacyEditionAsync(Uuid tenantId, Uuid editionId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var edition = await GetEditionAsync(editionId, ct).ConfigureAwait(false);
        return !edition.IsSuccess
            ? Result<CriteriaCatalogEdition>.Failure(edition.Error!)
            : Result<CriteriaCatalogEdition>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The edition metadata and disclosures have not been read from one qualified snapshot.", true));
    }

    ValueTask<Result<RuntimeCriteriaEdition>> GetEditionAsync(Uuid editionId, CancellationToken ct = default);

    ValueTask<Result<RuntimeCriteriaEntry>> GetEntryAsync(Uuid editionId, string identifier,
        CancellationToken ct = default);

    ValueTask<Result<RuntimeCriteriaEditionsPage>> ListEditionsAsync(Uuid tenantId, int? limit,
        string? cursor, CancellationToken ct = default);

    ValueTask<Result<RuntimeCriteriaDisclosurePage>> ListDisclosuresAsync(Uuid tenantId, Uuid editionId,
        int? limit, string? cursor, CancellationToken ct = default);

    ValueTask<Result<RuntimeCriteriaPage>> ListEntriesAsync(Uuid tenantId, Uuid editionId,
        string? category, string? kind, string? parentIdentifier, int? limit, string? cursor,
        CancellationToken ct = default);
}
