using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

sealed partial class FitzRuntimeCriteriaCatalog
{
    const string Live = "live";

    public async ValueTask<Result<RuntimeCriteriaEdition>> GetEditionAsync(Uuid editionId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, editionId), ct).ConfigureAwait(false);
        if (!Published(edition))
            return Unavailable<RuntimeCriteriaEdition>();
        var complete = EditionCovered(clock, edition!);
        return Result<RuntimeCriteriaEdition>.Success(ToEdition(edition!, complete));
    }

    public async ValueTask<Result<RuntimeCriteriaEntry>> GetEntryAsync(Uuid editionId, string identifier,
        CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty || !CriteriaContentBounds.Identifier(identifier))
            return Invalid<RuntimeCriteriaEntry>();
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, editionId), ct).ConfigureAwait(false);
        if (!Published(edition))
            return Unavailable<RuntimeCriteriaEntry>();
        var complete = EditionCovered(clock, edition!);
        var row = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EntryKey(Live, editionId, identifier), ct).ConfigureAwait(false);
        if (row is null)
        {
            if (!complete)
                return Unavailable<RuntimeCriteriaEntry>();
            return Result<RuntimeCriteriaEntry>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion was not found in the published edition."));
        }
        if (!complete || row.Pending?.BeforePublication == true)
            return Result<RuntimeCriteriaEntry>.Success(ToEntry(row, "pending"));
        RuntimeCriteriaEntryIndexRow? parent = null;
        if (row.ParentIdentifier is not null)
            parent = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(tx,
                new RuntimeCriteriaProjectionSchema.EntryKey(Live, editionId, row.ParentIdentifier), ct)
                .ConfigureAwait(false);
        return Result<RuntimeCriteriaEntry>.Success(ToEntry(row,
            RuntimeCriteriaProjectionRules.IsEffective(row, parent) ? "accepted" : "ineffective"));
    }

    public async ValueTask<Result<RuntimeCriteriaEditionsPage>> ListEditionsAsync(Uuid tenantId, int? limit,
        string? cursor, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || !ValidLimit(limit))
            return Invalid<RuntimeCriteriaEditionsPage>();
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        if (clock is null)
            return Unavailable<RuntimeCriteriaEditionsPage>();
        var seedId = PlatformCriteriaCatalog.SeedEdition().EditionId;
        var seed = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, seedId), ct).ConfigureAwait(false);
        var directoryCoverage = seed is not null && Published(seed) && EditionCovered(clock, seed) ? "complete" : "pending";
        string? inner = null;
        if (cursor is not null)
        {
            if (!RuntimeCriteriaPageCursor.TryDecode(cursor, out var parsed) || parsed.Kind != "editions" ||
                parsed.TenantId != tenantId || parsed.EditionId != Uuid.Empty ||
                parsed.PublicationEventId != Uuid.Empty || parsed.Category != "" ||
                parsed.EntryKind != "" || parsed.ParentIdentifier != "")
                return Invalid<RuntimeCriteriaEditionsPage>();
            if (!RuntimeCriteriaPageCursor.IsCurrent(parsed, clock.Epoch, clock.DirectoryRevision))
                return SnapshotChanged<RuntimeCriteriaEditionsPage>();
            inner = parsed.Inner;
        }
        var page = await RuntimeCriteriaProjectionSchema.Editions.QueryAsync(tx,
            RuntimeCriteriaProjectionSchema.ByPublication.Query().WithPrefix(Live, "published")
                .Take(limit ?? 50).After(inner), ct).ConfigureAwait(false);
        if (cursor is null && page.Items.Count == 0 && page.NextCursor is null)
            return Unavailable<RuntimeCriteriaEditionsPage>();
        // Each row's status is relative to this same projection snapshot.
        var items = page.Items.Select(row => ToEdition(row, EditionCovered(clock, row))).ToArray();
        var next = page.NextCursor is null ? null : RuntimeCriteriaPageCursor.Encode(new("editions", tenantId,
            Uuid.Empty, Uuid.Empty, clock.Epoch, "", "", "", clock.DirectoryRevision, page.NextCursor));
        return Result<RuntimeCriteriaEditionsPage>.Success(new(clock.Epoch, items, next, directoryCoverage));
    }

    public async ValueTask<Result<RuntimeCriteriaPage>> ListEntriesAsync(Uuid tenantId, Uuid editionId,
        string? category, string? kind, string? parentIdentifier, int? limit, string? cursor,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || editionId == Uuid.Empty || !ValidLimit(limit) ||
            category is not (null or "security" or "availability" or "confidentiality" or "processing_integrity" or "privacy") ||
            kind is not (null or "criterion" or "point_of_focus") ||
            parentIdentifier is not null && !CriteriaContentBounds.Identifier(parentIdentifier))
            return Invalid<RuntimeCriteriaPage>();
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, editionId), ct).ConfigureAwait(false);
        if (!Published(edition))
            return Unavailable<RuntimeCriteriaPage>();
        if (clock is null || !EditionCovered(clock, edition!))
            return Result<RuntimeCriteriaPage>.Success(new(editionId, edition!.PublicationEventId,
                clock?.Epoch ?? Live, "pending", [], null));
        string? inner = null;
        if (cursor is not null)
        {
            if (!RuntimeCriteriaPageCursor.TryDecode(cursor, out var parsed) || parsed.Kind != "entries" ||
                parsed.TenantId != tenantId || parsed.EditionId != editionId ||
                parsed.PublicationEventId != edition!.PublicationEventId ||
                parsed.Category != (category ?? "") || parsed.EntryKind != (kind ?? "") ||
                parsed.ParentIdentifier != (parentIdentifier ?? ""))
                return Invalid<RuntimeCriteriaPage>();
            if (!RuntimeCriteriaPageCursor.IsCurrent(parsed, clock.Epoch, edition.Revision))
                return SnapshotChanged<RuntimeCriteriaPage>();
            inner = parsed.Inner;
        }
        var page = await RuntimeCriteriaProjectionSchema.Entries.QueryAsync(tx,
            RuntimeCriteriaProjectionSchema.ByEditionIdentifier.Query().WithPrefix(Live, editionId.ToString())
                .Take(limit ?? 50).After(inner), ct).ConfigureAwait(false);
        var items = new List<RuntimeCriteriaEntry>();
        foreach (var row in page.Items)
        {
            if (category is not null && row.Category != category || kind is not null && row.Kind != kind ||
                parentIdentifier is not null && row.ParentIdentifier != parentIdentifier)
                continue;
            RuntimeCriteriaEntryIndexRow? parent = null;
            if (row.ParentIdentifier is not null)
                parent = await RuntimeCriteriaProjectionSchema.Entries.GetAsync(tx,
                    new RuntimeCriteriaProjectionSchema.EntryKey(Live, editionId, row.ParentIdentifier), ct)
                    .ConfigureAwait(false);
            if (RuntimeCriteriaProjectionRules.IsEffective(row, parent))
                items.Add(ToEntry(row, "accepted"));
        }
        var next = page.NextCursor is null ? null : RuntimeCriteriaPageCursor.Encode(new("entries", tenantId,
            editionId, edition!.PublicationEventId!.Value, clock.Epoch, category ?? "", kind ?? "",
            parentIdentifier ?? "", edition.Revision, page.NextCursor));
        return Result<RuntimeCriteriaPage>.Success(new(editionId, edition!.PublicationEventId, clock.Epoch,
            "complete", items, next));
    }

    public async ValueTask<Result<RuntimeCriteriaDisclosurePage>> ListDisclosuresAsync(Uuid tenantId,
        Uuid editionId, int? limit, string? cursor, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || editionId == Uuid.Empty || !ValidLimit(limit))
            return Invalid<RuntimeCriteriaDisclosurePage>();
        await using var tx = await BeginReadAsync("bdgrz", ct).ConfigureAwait(false);
        var clock = await RuntimeCriteriaProjectionSchema.Clock.GetAsync(tx, Live, ct).ConfigureAwait(false);
        var edition = await RuntimeCriteriaProjectionSchema.Editions.GetAsync(tx,
            new RuntimeCriteriaProjectionSchema.EditionKey(Live, editionId), ct).ConfigureAwait(false);
        if (edition is null || !Published(edition))
            return Unavailable<RuntimeCriteriaDisclosurePage>();
        if (clock is null || !EditionCovered(clock, edition!))
            return Result<RuntimeCriteriaDisclosurePage>.Success(new(editionId, clock?.Epoch ?? Live,
                "pending", [], null));
        string? inner = null;
        if (cursor is not null)
        {
            if (!RuntimeCriteriaPageCursor.TryDecode(cursor, out var parsed) || parsed.Kind != "disclosures" ||
                parsed.TenantId != tenantId || parsed.EditionId != editionId ||
                parsed.PublicationEventId != edition.PublicationEventId || parsed.Category != "" ||
                parsed.EntryKind != "" || parsed.ParentIdentifier != "")
                return Invalid<RuntimeCriteriaDisclosurePage>();
            if (!RuntimeCriteriaPageCursor.IsCurrent(parsed, clock.Epoch, edition.Revision))
                return SnapshotChanged<RuntimeCriteriaDisclosurePage>();
            inner = parsed.Inner;
        }
        var page = await RuntimeCriteriaProjectionSchema.Disclosures.QueryAsync(tx,
            RuntimeCriteriaProjectionSchema.ByEditionDisclosure.Query().WithPrefix(Live, editionId.ToString())
                .Take(limit ?? 50).After(inner), ct).ConfigureAwait(false);
        var items = page.Items.Select(row => new RuntimeCriteriaDisclosure(row.EditionId, row.Category,
            row.Code, row.Note)).ToArray();
        var next = page.NextCursor is null ? null : RuntimeCriteriaPageCursor.Encode(new("disclosures", tenantId,
            editionId, edition.PublicationEventId!.Value, clock.Epoch, "", "", "", edition.Revision, page.NextCursor));
        return Result<RuntimeCriteriaDisclosurePage>.Success(new(editionId, clock.Epoch, "complete", items, next));
    }

    // The projection and its checkpoint commit together. Completeness here is
    // relative to that observed source snapshot, not proof of a current head.
    static bool EditionCovered(RuntimeCriteriaProjectionClock? clock, RuntimeCriteriaEditionIndexRow edition) =>
        clock is not null && edition.PendingBeforeCut == 0 && edition.PublicationEventId is not null &&
        (edition.SeedVersion is null && edition.SeedFingerprint is null || edition.SeedImportCompletionEventId is not null);

    static bool ValidLimit(int? limit) => limit is null or >= 1 and <= 200;
    static bool Published(RuntimeCriteriaEditionIndexRow? edition) =>
        edition?.Status == "published" && edition.PublicationEventId is not null;

    static RuntimeCriteriaEdition ToEdition(RuntimeCriteriaEditionIndexRow edition, bool complete) => new(
        edition.EditionId, edition.DraftGeneration, edition.Status, edition.Label,
        edition.PublicationEventId, edition.PublishedAt, complete ? "complete" : "pending",
        edition.Framework, complete ? edition.CoverageNote : null, complete ? edition.SourceUrl : null,
        complete ? edition.ContentRights : null, complete ? edition.NumberedCriteriaComplete : null);

    static RuntimeCriteriaEntry ToEntry(RuntimeCriteriaEntryIndexRow row, string status) => status == "pending"
        ? new(row.EditionId, row.Identifier, status, null, null, null, null, null, null)
        : new(row.EditionId, row.Identifier, status, row.IncarnationId, row.Kind, row.Category,
            row.ParentIdentifier, row.ParentIncarnationId, row.Summary);

    static Result<T> Invalid<T>() => Result<T>.Failure(new RequestError(RequestErrorKind.Validation,
        "The criteria read request or continuation is invalid."));
    static Result<T> Unavailable<T>() => Result<T>.Failure(new RequestError(RequestErrorKind.Conflict,
        "The published criteria source has not been fully qualified.", isTransient: true));
    static Result<T> SnapshotChanged<T>() => Result<T>.Failure(new RequestError(RequestErrorKind.Conflict,
        "The criteria read changed during pagination; restart from the first page.", isTransient: true));
}
