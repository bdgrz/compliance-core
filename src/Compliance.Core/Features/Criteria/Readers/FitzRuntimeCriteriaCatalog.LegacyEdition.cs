using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

sealed partial class FitzRuntimeCriteriaCatalog
{
    // The legacy response is an unpaged array. Larger sets belong to the paged
    // disclosure API; neither limit constrains the source owners or their streams.
    internal const int LegacyDisclosureLimit = 32;
    internal const int LegacyResponseByteLimit = 16 * 1024;
    internal const int LegacyEditionLimit = 32;

    public async ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> ListLegacyEditionsAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty)
            return Invalid<IReadOnlyList<CriteriaCatalogEdition>>();

        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        if (clock is null)
            return Unavailable<IReadOnlyList<CriteriaCatalogEdition>>();
        var seed = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, PlatformCriteriaCatalog.SeedEdition().EditionId), ct)
            .ConfigureAwait(false);
        if (seed is null || !Published(seed) || !EditionCovered(clock, seed))
            return Unavailable<IReadOnlyList<CriteriaCatalogEdition>>();

        var items = new List<CriteriaCatalogEdition>();
        long budget = 2;
        string? cursor = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await RuntimeCriteriaProjectionSchema.Editions.QueryAsync(tx,
                RuntimeCriteriaProjectionSchema.ByPublication.Query().WithPrefix(Live, "published")
                    .Take(1).After(cursor), ct).ConfigureAwait(false);
            foreach (var edition in page.Items)
            {
                if (items.Count == LegacyEditionLimit)
                    return LegacyDirectoryTooLarge();
                var detail = await ReadLegacyEditionAsync(tx, clock, edition, ct).ConfigureAwait(false);
                if (!detail.IsSuccess)
                    return Result<IReadOnlyList<CriteriaCatalogEdition>>.Failure(detail.Error!);
                var item = detail.Value!;
                budget += 512 + EncodedUpperBound(item.Framework, item.EditionLabel, item.CoverageNote,
                    item.SourceUrl, item.ContentRights);
                foreach (var gap in item.SupportGaps)
                    budget += 64 + EncodedUpperBound(gap.Category, gap.Code, gap.Note);
                if (budget > LegacyResponseByteLimit)
                    return LegacyDirectoryTooLarge();
                items.Add(item);
            }
            if (page.Items.Count == 0 && page.NextCursor is not null)
                throw new InvalidOperationException("The edition page did not make progress.");
            cursor = page.NextCursor;
        } while (cursor is not null);

        if (items.Count == 0)
            return Unavailable<IReadOnlyList<CriteriaCatalogEdition>>();
        items.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.EditionLabel, b.EditionLabel));
        return Result<IReadOnlyList<CriteriaCatalogEdition>>.Success(items);
    }

    public async ValueTask<Result<CriteriaCatalogEdition>> GetLegacyEditionAsync(Uuid tenantId, Uuid editionId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || editionId == Uuid.Empty)
            return Invalid<CriteriaCatalogEdition>();

        // The edition, coverage checkpoint, and every disclosure come from the
        // same committed KV snapshot. Separate page/edition calls can race.
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, editionId), ct).ConfigureAwait(false);
        if (edition is null || clock is null)
            return Unavailable<CriteriaCatalogEdition>();
        return await ReadLegacyEditionAsync(tx, clock, edition, ct).ConfigureAwait(false);
    }

    static async ValueTask<Result<CriteriaCatalogEdition>> ReadLegacyEditionAsync(IKvTransaction tx,
        RuntimeCriteriaProjectionClock clock, RuntimeCriteriaEditionIndexRow edition, CancellationToken ct)
    {
        var editionId = edition.EditionId;
        if (!Published(edition) || !EditionCovered(clock, edition))
            return Unavailable<CriteriaCatalogEdition>();
        if (string.IsNullOrWhiteSpace(edition.Framework) || string.IsNullOrWhiteSpace(edition.Label) ||
            edition.PublishedAt is null || edition.NumberedCriteriaComplete is null || edition.CoverageNote is null ||
            edition.SourceUrl is null || edition.ContentRights is null)
            return Unavailable<CriteriaCatalogEdition>();

        // JSON escapes can use up to six bytes per UTF-16 code unit. This upper
        // bound includes field overhead, so the unpaged response stays small.
        var budget = 512 + EncodedUpperBound(edition.Framework, edition.Label, edition.CoverageNote,
            edition.SourceUrl, edition.ContentRights);
        if (budget > LegacyResponseByteLimit)
            return LegacyResponseTooLarge();

        var gaps = new List<CriteriaSupportGap>();
        string? cursor = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            // Read one record at a time: a malformed or unusually large source
            // record cannot force the reader to materialize an entire page.
            var page = await RuntimeCriteriaProjectionSchema.Disclosures.QueryAsync(tx,
                RuntimeCriteriaProjectionSchema.ByEditionDisclosure.Query().WithPrefix(Live, editionId.ToString())
                    .Take(1).After(cursor), ct).ConfigureAwait(false);
            foreach (var row in page.Items)
            {
                if (row.EditionId != editionId || row.Generation != Live ||
                    string.IsNullOrWhiteSpace(row.Category) || string.IsNullOrWhiteSpace(row.Code) || row.Note is null)
                    throw new InvalidOperationException("A projected disclosure does not match its edition or source fields.");
                budget += 64 + EncodedUpperBound(row.Category, row.Code, row.Note);
                if (gaps.Count == LegacyDisclosureLimit || budget > LegacyResponseByteLimit)
                    return LegacyResponseTooLarge();
                gaps.Add(new CriteriaSupportGap(row.Category, row.Code, row.Note));
            }
            if (page.Items.Count == 0 && page.NextCursor is not null)
                throw new InvalidOperationException("The disclosure page did not make progress.");
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<CriteriaCatalogEdition>.Success(new CriteriaCatalogEdition(editionId, edition.Framework,
            edition.Label, edition.PublishedAt.Value, edition.NumberedCriteriaComplete.Value, edition.CoverageNote,
            edition.SourceUrl, edition.ContentRights, gaps));
    }

    static long EncodedUpperBound(params string[] values) => values.Sum(static value => (long)value.Length * 6);

    static Result<CriteriaCatalogEdition> LegacyResponseTooLarge() =>
        Result<CriteriaCatalogEdition>.Failure(new RequestError(RequestErrorKind.Conflict,
            "This edition's disclosures exceed the legacy response limit; use the paged disclosure API."));

    static Result<IReadOnlyList<CriteriaCatalogEdition>> LegacyDirectoryTooLarge() =>
        Result<IReadOnlyList<CriteriaCatalogEdition>>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The edition directory exceeds the legacy response limit; use the paged editions API."));
}
