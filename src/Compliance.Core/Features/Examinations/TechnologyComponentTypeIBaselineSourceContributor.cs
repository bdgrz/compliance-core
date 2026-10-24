using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures component revisions included by the exact approved Type I boundary.</summary>
public sealed class TechnologyComponentTypeIBaselineSourceContributor(
    IAggregateReader reader, IDomainEventReader events,
    TechnologyInventoryReadConsistency consistency, IBoundaryDirectoryReader boundaries,
    ProgramSetupWorkReadConsistency boundaryConsistency) : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.system_component_revision.v1\0";

    public string Family => "system_components";

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
                "A system-component baseline read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Success([], ["System components were not read from the engagement's exact approved boundary."], []);

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
            return Success([], ["The system-component scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundaries[0].ContentSha256))
            return BoundaryChanged();

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var componentIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var entry in boundaryVersion.Content.Entries.Where(static entry =>
                     entry.SubjectType == "component" && entry.Kind == "inclusion"))
        {
            if (entry.Unresolved || entry.GovernedRecordId is not { } componentId ||
                componentId == Uuid.Empty)
            {
                blockers.Add("An included component in the approved boundary cannot be resolved.");
                scopeDifferences.Add($"Boundary scope entry {entry.EntryId} does not identify a resolved component.");
                continue;
            }
            componentIds.Add(componentId);
        }

        var sources = new List<TypeIBaselineSourceReference>();
        foreach (var componentId in componentIds)
        {
            var currentResult = await consistency.GetComponentAsync(engagement.TenantId,
                componentId, null, ct).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                if (currentResult.Error.Kind == RequestErrorKind.NotFound)
                {
                    blockers.Add("A component included by the approved boundary has no source history.");
                    continue;
                }
                return Result<TypeIBaselineSourceContribution>.Failure(currentResult.Error);
            }

            var current = currentResult.Value;
            if (!IsValidDirectoryView(current, engagement.TenantId, componentId))
                return InvalidDirectory();

            var aggregate = await reader.HydrateAsync(
                new TechnologyComponent(engagement.TenantId, componentId), ct).ConfigureAwait(false);
            var history = await ReadHistoryAsync(engagement.TenantId, componentId, ct)
                .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (!aggregate.IsCreated || aggregate.Id != componentId ||
                aggregate.Revision != current.Revision || aggregate.Revision != history.Value.Count ||
                !MatchesCurrent(current, aggregate, history.Value))
                return SourceConflict();

            var version = history.Value.LastOrDefault(revision => revision.ChangedAt <= through);
            if (version is null)
            {
                blockers.Add("A component included by the approved boundary has no revision recorded by the Type I date.");
                continue;
            }

            try
            {
                sources.Add(new TypeIBaselineSourceReference("system_component_revision", componentId,
                    RevisionVersionId(componentId, version.Revision), version.Revision,
                    Digest(engagement.TenantId, componentId, version)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("A component has invalid canonical source content.");
                scopeDifferences.Add($"Component {componentId} has invalid canonical source content.");
                continue;
            }

            if (version.Content.Lifecycle == TechnologyInventoryRules.Retired)
                scopeDifferences.Add($"Component {componentId} was retired on or before the Type I date.");

            if (version.Content.Category == TechnologyInventoryRules.CloudAccount &&
                !HasReviewedSystemSource(context.PreviouslyCapturedSources,
                    version.Content.SystemInstanceId!.Value))
            {
                blockers.Add("A cloud account component's reviewed system was not captured by the Type I baseline.");
                scopeDifferences.Add($"Cloud account component {componentId} has no matching reviewed-system source reference.");
            }
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

    async ValueTask<Result<IReadOnlyList<TechnologyComponentRevisionRecorded>>> ReadHistoryAsync(
        Uuid tenantId, Uuid componentId, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), TechnologyInventoryStreams.Area,
            componentId.ToString());
        var history = new List<TechnologyComponentRevisionRecorded>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.Event is not TechnologyComponentRevisionRecorded revision ||
                !IsValid(revision, tenantId, componentId, history.Count + 1) ||
                history.Count == 0 && revision.Content.Lifecycle != TechnologyInventoryRules.Active ||
                history.Count > 0 && (revision.Content.Category != history[0].Content.Category ||
                    revision.Content.SystemInstanceId != history[0].Content.SystemInstanceId))
                return InvalidHistory();
            history.Add(revision);
        }
        return history.Count == 0
            ? InvalidHistory()
            : Result<IReadOnlyList<TechnologyComponentRevisionRecorded>>.Success(history.AsReadOnly());
    }

    static bool IsValid(TechnologyComponentRevisionRecorded revision, Uuid tenantId,
        Uuid componentId, long expectedRevision)
    {
        if (revision.TenantId != tenantId || revision.ComponentId != componentId ||
            revision.Revision != expectedRevision || revision.Content is null ||
            revision.Actor is null || revision.ChangedAt == default ||
            !ValidText(revision.Actor.Kind, 200) || !ValidText(revision.Actor.Id, 200) ||
            !ValidText(revision.Actor.Display, 200))
            return false;
        var normalized = TechnologyInventoryRules.Normalize(revision.Content);
        return normalized == revision.Content && TechnologyInventoryRules.Validate(normalized) is null &&
               (normalized.Category != TechnologyInventoryRules.CloudAccount ||
                normalized.SystemInstanceId == componentId);
    }

    static bool IsValidDirectoryView(TechnologyComponentView view, Uuid tenantId, Uuid componentId) =>
        view.TenantId == tenantId && view.ComponentId == componentId && view.Revision >= 1 &&
        view.SourceKind == TechnologyInventoryStreams.ManualSource && view.Content is not null &&
        TechnologyInventoryRules.Normalize(view.Content) == view.Content &&
        TechnologyInventoryRules.Validate(view.Content) is null &&
        (view.Content.Category != TechnologyInventoryRules.CloudAccount ||
         view.Content.SystemInstanceId == componentId) && view.LastChangedAt != default &&
        view.LastChangedBy is { } actor && ValidText(actor.Kind, 200) &&
        ValidText(actor.Id, 200) && ValidText(actor.Display, 200);

    static bool MatchesCurrent(TechnologyComponentView current, TechnologyComponent aggregate,
        IReadOnlyList<TechnologyComponentRevisionRecorded> history)
    {
        var latest = history[^1];
        return aggregate.Content == latest.Content && current.Revision == latest.Revision &&
               current.Content == latest.Content && current.LastChangedBy == latest.Actor &&
               current.LastChangedAt == latest.ChangedAt;
    }

    static bool HasReviewedSystemSource(IReadOnlyList<TypeIBaselineSourceReference> sources,
        Uuid systemInstanceId)
    {
        var matches = sources.Where(source => source.Kind == "reviewed_system_revision" &&
            source.RecordId == systemInstanceId).ToArray();
        return matches.Length == 1 && matches[0].Revision >= 1 &&
               matches[0].VersionId == Uuid.CreateVersion5(systemInstanceId,
                   $"reviewed_system_revision_v1:{matches[0].Revision}") &&
               IsSha256(matches[0].ContentSha256);
    }

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static Uuid RevisionVersionId(Uuid componentId, long revision) =>
        Uuid.CreateVersion5(componentId, $"technology_component_revision_v1:{revision}");

    static string Digest(Uuid tenantId, Uuid componentId,
        TechnologyComponentRevisionRecorded revision)
    {
        var content = new TechnologyComponentSourceContent(tenantId, componentId,
            revision.Revision, revision.Content, revision.Actor, revision.ChangedAt);
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.TechnologyComponentSourceContent);
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

    static bool IsSha256(string value) => value.Length == 64 &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<IReadOnlyList<TechnologyComponentRevisionRecorded>> InvalidHistory() =>
        Result<IReadOnlyList<TechnologyComponentRevisionRecorded>>.Failure(new RequestError(
            RequestErrorKind.Conflict,
            "The technology-component source history is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while system-component scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The technology-inventory directory returned invalid baseline source data."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The technology-component source or projection changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            "system_components", sources, blockers, scopeDifferences));
}
