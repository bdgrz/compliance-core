using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures governed information-asset revisions in the exact approved Type I boundary.</summary>
public sealed class InformationAssetTypeIBaselineSourceContributor(
    IAggregateReader reader, IDomainEventReader events,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility, IBoundaryDirectoryReader boundaries,
    ProgramSetupWorkReadConsistency boundaryConsistency) : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.information_asset_revision.v1\0";

    public string Family => "information_assets";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "An information-asset baseline read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Success([], ["Information assets were not read from the engagement's exact approved boundary."], []);

        var boundaryFence = await boundaryConsistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryFence.Error);
        var inventoryFence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!inventoryFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(inventoryFence.Error);

        var boundary = await boundaries.GetAsync(engagement.TenantId, engagement.BoundaryId, ct)
            .ConfigureAwait(false);
        var boundaryVersion = await boundaries.GetEffectiveVersionAsync(engagement.TenantId,
            engagement.BoundaryId, engagement.PeriodStart, ct).ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != engagement.TenantId ||
            boundary.ProgramId != engagement.ProgramId || boundary.BoundaryId != engagement.BoundaryId ||
            boundaryVersion is null || boundaryVersion.TenantId != engagement.TenantId ||
            boundaryVersion.ProgramId != engagement.ProgramId ||
            boundaryVersion.BoundaryId != engagement.BoundaryId ||
            boundaryVersion.VersionId != priorBoundaries[0].VersionId ||
            boundaryVersion.Revision != priorBoundaries[0].Revision ||
            boundaryVersion.Status != "approved" ||
            boundaryVersion.EffectiveFrom is not { } effectiveFrom ||
            effectiveFrom > engagement.PeriodStart)
            return BoundaryChanged();

        string boundaryDigest;
        try
        {
            boundaryDigest = SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion);
        }
        catch (SnapshotContentException)
        {
            return Success([], ["The information-asset scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundaries[0].ContentSha256))
            return BoundaryChanged();

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var assetIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var entry in boundaryVersion.Content.Entries.Where(static entry =>
                     entry.SubjectType == "information" && entry.Kind == "inclusion"))
        {
            if (entry.Unresolved || entry.GovernedRecordId is not { } assetId ||
                assetId == Uuid.Empty)
            {
                blockers.Add("An included information asset in the approved boundary cannot be resolved.");
                scopeDifferences.Add($"Boundary scope entry {entry.EntryId} does not identify a resolved information asset.");
                continue;
            }
            assetIds.Add(assetId);
        }

        var sources = new List<TypeIBaselineSourceReference>();
        foreach (var assetId in assetIds)
        {
            var currentResult = await consistency.GetAssetAsync(engagement.TenantId, assetId,
                null, ct).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                if (currentResult.Error.Kind == RequestErrorKind.NotFound)
                {
                    blockers.Add("An information asset included by the approved boundary has no source history.");
                    continue;
                }
                return Result<TypeIBaselineSourceContribution>.Failure(currentResult.Error);
            }

            var current = currentResult.Value;
            if (!IsValidDirectoryView(current, engagement.TenantId, assetId))
                return InvalidDirectory();

            var aggregate = await reader.HydrateAsync(
                new InformationAsset(engagement.TenantId, assetId), ct).ConfigureAwait(false);
            var history = await ReadHistoryAsync(engagement.TenantId, assetId, ct)
                .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (!aggregate.IsCreated || aggregate.Id != assetId ||
                aggregate.Revision != current.Revision || aggregate.Revision != history.Value.Count ||
                !MatchesCurrent(current, aggregate, history.Value))
                return SourceConflict();

            var eligibleHistory = history.Value;
            if (context.RecordedThrough is not null)
            {
                var receipts = await context.RequireReceipts(engagement.TenantId).ReadAsync(new(engagement.TenantId.ToString(),
                    TechnologyInventoryStreams.Area, assetId.ToString()), ct).ConfigureAwait(false);
                eligibleHistory = history.Value.Where(revision => receipts.Includes(revision)).ToArray();
            }
            else
                eligibleHistory = history.Value.Where(revision => revision.ChangedAt <= through).ToArray();
            var version = eligibleHistory.Count == 0 ? null : eligibleHistory[^1];
            if (version is null)
            {
                blockers.Add("An information asset included by the approved boundary has no revision recorded by the Type I date.");
                continue;
            }

            var readable = await visibility.CanReadAssetAsync(engagement.TenantId,
                context.RequesterUserId, assetId, version.Content.Classification, ct)
                .ConfigureAwait(false);
            if (!readable)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Forbidden,
                    "The requester is not authorized to read every information asset in the Type I boundary."));

            try
            {
                sources.Add(new TypeIBaselineSourceReference("information_asset_revision", assetId,
                    RevisionVersionId(assetId, version.Revision), version.Revision,
                    Digest(engagement.TenantId, assetId, version)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("An information asset has invalid canonical source content.");
                scopeDifferences.Add($"Information asset {assetId} has invalid canonical source content.");
                continue;
            }

            if (version.Content.Lifecycle == TechnologyInventoryRules.Retired)
                scopeDifferences.Add($"Information asset {assetId} was retired on or before the Type I date.");
        }

        var inventoryConfirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, inventoryFence.Value, ct).ConfigureAwait(false);
        if (!inventoryConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(inventoryConfirmed.Error);
        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryConfirmed.Error);

        return Success(sources, blockers, scopeDifferences);
    }

    async ValueTask<Result<IReadOnlyList<InformationAssetRevisionRecorded>>> ReadHistoryAsync(
        Uuid tenantId, Uuid assetId, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), TechnologyInventoryStreams.Area,
            assetId.ToString());
        var history = new List<InformationAssetRevisionRecorded>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.Event is not InformationAssetRevisionRecorded revision ||
                !IsValid(revision, tenantId, assetId, history.Count + 1) ||
                history.Count == 0 && revision.Content.Lifecycle != TechnologyInventoryRules.Active)
                return InvalidHistory();
            history.Add(revision);
        }
        return history.Count == 0
            ? InvalidHistory()
            : Result<IReadOnlyList<InformationAssetRevisionRecorded>>.Success(history.AsReadOnly());
    }

    static bool IsValid(InformationAssetRevisionRecorded revision, Uuid tenantId, Uuid assetId,
        long expectedRevision)
    {
        if (revision.TenantId != tenantId || revision.InformationAssetId != assetId ||
            revision.Revision != expectedRevision || revision.Content is null ||
            revision.Actor is null || revision.ChangedAt == default ||
            !ValidText(revision.Actor.Kind, 200) || !ValidText(revision.Actor.Id, 200) ||
            !ValidText(revision.Actor.Display, 200))
            return false;
        var normalized = TechnologyInventoryRules.Normalize(revision.Content);
        return normalized == revision.Content && TechnologyInventoryRules.Validate(normalized) is null;
    }

    static bool IsValidDirectoryView(InformationAssetView view, Uuid tenantId, Uuid assetId) =>
        view.TenantId == tenantId && view.InformationAssetId == assetId && view.Revision >= 1 &&
        view.SourceKind == TechnologyInventoryStreams.ManualSource && view.Content is not null &&
        TechnologyInventoryRules.Normalize(view.Content) == view.Content &&
        TechnologyInventoryRules.Validate(view.Content) is null && view.LastChangedAt != default &&
        view.LastChangedBy is { } actor && ValidText(actor.Kind, 200) &&
        ValidText(actor.Id, 200) && ValidText(actor.Display, 200);

    static bool MatchesCurrent(InformationAssetView current, InformationAsset aggregate,
        IReadOnlyList<InformationAssetRevisionRecorded> history)
    {
        var latest = history[^1];
        return aggregate.Content == latest.Content && current.Revision == latest.Revision &&
               current.Content == latest.Content && current.LastChangedBy == latest.Actor &&
               current.LastChangedAt == latest.ChangedAt;
    }

    static Uuid RevisionVersionId(Uuid assetId, long revision) =>
        Uuid.CreateVersion5(assetId, $"information_asset_revision_v1:{revision}");

    static string Digest(Uuid tenantId, Uuid assetId,
        InformationAssetRevisionRecorded revision)
    {
        var content = new InformationAssetSourceContent(tenantId, assetId, revision.Revision,
            revision.Content, revision.Actor, revision.ChangedAt);
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.InformationAssetSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static Result<IReadOnlyList<InformationAssetRevisionRecorded>> InvalidHistory() =>
        Result<IReadOnlyList<InformationAssetRevisionRecorded>>.Failure(new RequestError(
            RequestErrorKind.Conflict,
            "The information-asset source history is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while information-asset scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The technology-inventory directory returned invalid information-asset baseline data."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The information-asset source or projection changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            "information_assets", sources, blockers, scopeDifferences));
}
