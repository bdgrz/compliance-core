using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Adapts a bounded runtime page to the legacy published-entry contract.</summary>
static class CriteriaCatalogEntriesPage
{
    public static async ValueTask<Result<Page<Criterion>>> ReadAsync(IRuntimeCriteriaCatalogReader catalog,
        ICriteriaTextOverlayReader overlays, Uuid tenantId, Uuid editionId, string? category,
        string? kind, string? parentIdentifier, int? limit, string? cursor,
        CriteriaTextOverlayPurpose purpose, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || editionId == Uuid.Empty || limit is < 1 or > 200 ||
            category is not null and not ("security" or "availability" or "confidentiality" or
                "processing_integrity" or "privacy") ||
            kind is not null and not ("criterion" or "point_of_focus") ||
            parentIdentifier is not null && !CriteriaContentBounds.Identifier(parentIdentifier))
            return Failure(RequestErrorKind.Validation, "The criteria list filter or limit is invalid.");

        var prefix = purpose == CriteriaTextOverlayPurpose.Export ? "criteria-export-v2:" : "criteria-display-v2:";
        string? sourceCursor = null;
        if (cursor is not null)
        {
            if (!cursor.StartsWith(prefix, StringComparison.Ordinal) || cursor.Length == prefix.Length)
                return Failure(RequestErrorKind.Validation,
                    "The criteria cursor is invalid for this operation; restart the listing.");
            sourceCursor = cursor[prefix.Length..];
        }

        var result = await catalog.ListEntriesAsync(tenantId, editionId, category, kind, parentIdentifier,
            limit, sourceCursor, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Result<Page<Criterion>>.Failure(result.Error!);

        var page = result.Value!;
        if (page.EditionId != editionId || page.Coverage != "complete" ||
            page.PublicationEventId is not { } publication || publication == Uuid.Empty ||
            page.Items.Count > (limit ?? 50))
            return Pending();

        var entries = new List<Criterion>(page.Items.Count);
        foreach (var item in page.Items)
        {
            if (!TryConvert(item, editionId, out var criterion))
                return Pending();
            entries.Add(criterion);
        }

        var projected = await overlays.ApplyPageAsync(tenantId, entries,
            purpose == CriteriaTextOverlayPurpose.Export, ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<Criterion>>.Failure(projected.Error!);
        return Result<Page<Criterion>>.Success(new Page<Criterion>(projected.Value!,
            page.NextCursor is null ? null : prefix + page.NextCursor));
    }

    internal static bool TryConvert(RuntimeCriteriaEntry item, Uuid editionId, out Criterion criterion)
    {
        criterion = null!;
        if (item.EditionId != editionId || item.Status != "accepted" ||
            item.IncarnationId is not { } incarnation || incarnation == Uuid.Empty ||
            string.IsNullOrWhiteSpace(item.Identifier) ||
            item.Kind is not ("criterion" or "point_of_focus") || string.IsNullOrWhiteSpace(item.Category) ||
            string.IsNullOrWhiteSpace(item.Summary) ||
            (item.Kind == "criterion" && (item.ParentIdentifier is not null || item.ParentIncarnationId is not null)) ||
            (item.Kind == "point_of_focus" &&
                (string.IsNullOrWhiteSpace(item.ParentIdentifier) || item.ParentIncarnationId is not { } parent ||
                    parent == Uuid.Empty)))
            return false;

        criterion = new Criterion(item.EditionId, item.Identifier,
            item.Kind == "criterion" ? item.Identifier : null, item.Category, item.Kind,
            item.ParentIdentifier, item.Summary);
        return true;
    }

    internal static Result<Page<Criterion>> Pending() => Failure(RequestErrorKind.Conflict,
        "Criteria coverage or a published entry outcome is not available yet.", true);

    static Result<Page<Criterion>> Failure(RequestErrorKind kind, string message, bool transient = false) =>
        Result<Page<Criterion>>.Failure(new RequestError(kind, message, transient));
}
