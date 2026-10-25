using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures point-in-time data-flow versions from the exact approved boundary.</summary>
public sealed class DataFlowTypeIBaselineSourceContributor(
    IAggregateReader reader, IDomainEventReader events,
    TechnologyInventoryReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility, IBoundaryDirectoryReader boundaries,
    ProgramSetupWorkReadConsistency boundaryConsistency) : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.data_flow_revision.v1\0";

    public string Family => "data_flows";

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
                "A data-flow baseline read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Success([], ["Data flows were not read from the engagement's exact approved boundary."], []);

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
            return Success([], ["The data-flow scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundaries[0].ContentSha256))
            return BoundaryChanged();

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var flowIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var entry in boundaryVersion.Content.Entries.Where(static entry =>
                     entry.SubjectType == "data_flow" && entry.Kind == "inclusion"))
        {
            if (entry.Unresolved || entry.GovernedRecordId is not { } flowId ||
                flowId == Uuid.Empty)
            {
                blockers.Add("An included data flow in the approved boundary cannot be resolved.");
                scopeDifferences.Add($"Boundary scope entry {entry.EntryId} does not identify a resolved data flow.");
                continue;
            }
            flowIds.Add(flowId);
        }

        var sources = new List<TypeIBaselineSourceReference>();
        foreach (var flowId in flowIds)
        {
            var currentResult = await consistency.GetFlowAsync(engagement.TenantId, flowId,
                null, ct).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                if (currentResult.Error.Kind == RequestErrorKind.NotFound)
                {
                    blockers.Add("A data flow included by the approved boundary has no source history.");
                    continue;
                }
                return Result<TypeIBaselineSourceContribution>.Failure(currentResult.Error);
            }

            var current = currentResult.Value;
            if (!IsValidDirectoryView(current, engagement.TenantId, flowId))
                return InvalidDirectory();

            var aggregate = await reader.HydrateAsync(new DataFlow(engagement.TenantId, flowId), ct)
                .ConfigureAwait(false);
            var history = await ReadHistoryAsync(engagement.TenantId, flowId, ct)
                .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (!aggregate.IsCreated || aggregate.Id != flowId ||
                aggregate.Revision != current.Revision || aggregate.Revision != history.Value.Count ||
                !MatchesCurrent(current, aggregate, history.Value))
                return SourceConflict();

            var eligibleHistory = history.Value;
            if (context.RecordedThrough is not null)
            {
                var receipts = await context.RequireReceipts(engagement.TenantId).ReadAsync(new(engagement.TenantId.ToString(),
                    TechnologyInventoryStreams.Area, flowId.ToString()), ct).ConfigureAwait(false);
                eligibleHistory = history.Value.Where(revision => receipts.Includes(revision)).ToArray();
            }
            else
                eligibleHistory = history.Value.Where(revision => revision.ChangedAt <= through).ToArray();
            var throughRevision = eligibleHistory.ToArray();
            var version = throughRevision.LastOrDefault(revision =>
                revision.Content.EffectiveFrom <= engagement.PeriodStart);
            if (version is null)
            {
                blockers.Add("A data flow included by the approved boundary has no version recorded and effective by the Type I date.");
                if (throughRevision.Any(revision =>
                        revision.Content.EffectiveFrom > engagement.PeriodStart))
                    scopeDifferences.Add($"Data flow {flowId} has no effective version on the Type I date and has a later scheduled version.");
                continue;
            }

            var readable = await visibility.CanReadFlowAsync(engagement.TenantId,
                context.RequesterUserId, flowId, version.Content.Classification, ct)
                .ConfigureAwait(false);
            if (!readable)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Forbidden,
                    "The requester is not authorized to read every data flow in the Type I boundary."));

            try
            {
                sources.Add(new TypeIBaselineSourceReference("data_flow_revision", flowId,
                    RevisionVersionId(flowId, version.Revision), version.Revision,
                    Digest(engagement.TenantId, flowId, version)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("A data flow has invalid canonical source content.");
                scopeDifferences.Add($"Data flow {flowId} has invalid canonical source content.");
                continue;
            }

            ValidateReferences(context.PreviouslyCapturedSources, flowId, version.Content,
                blockers, scopeDifferences);
            if (version.Content.Lifecycle == TechnologyInventoryRules.Retired)
                scopeDifferences.Add($"Data flow {flowId} was retired on or before the Type I date.");
            if (throughRevision.Any(revision => revision.Revision > version.Revision &&
                    revision.Content.EffectiveFrom > engagement.PeriodStart))
                scopeDifferences.Add($"Data flow {flowId} has a version scheduled after the Type I date.");
            if (history.Value.Any(revision => !eligibleHistory.Contains(revision) &&
                    revision.Content.EffectiveFrom <= engagement.PeriodStart))
                scopeDifferences.Add($"Data flow {flowId} was revised after the Type I date with an effective date on or before it.");
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

    async ValueTask<Result<IReadOnlyList<DataFlowRevisionRecorded>>> ReadHistoryAsync(
        Uuid tenantId, Uuid flowId, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), TechnologyInventoryStreams.Area,
            flowId.ToString());
        var history = new List<DataFlowRevisionRecorded>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.Event is not DataFlowRevisionRecorded revision ||
                !IsValid(revision, tenantId, flowId, history.Count + 1) ||
                history.Count == 0 && revision.Content.Lifecycle != TechnologyInventoryRules.Active ||
                history.Count > 0 && revision.Content.EffectiveFrom < history[^1].Content.EffectiveFrom)
                return InvalidHistory();
            history.Add(revision);
        }
        return history.Count == 0
            ? InvalidHistory()
            : Result<IReadOnlyList<DataFlowRevisionRecorded>>.Success(history.AsReadOnly());
    }

    static bool IsValid(DataFlowRevisionRecorded revision, Uuid tenantId, Uuid flowId,
        long expectedRevision)
    {
        if (revision.TenantId != tenantId || revision.DataFlowId != flowId ||
            revision.Revision != expectedRevision || revision.Content is null ||
            revision.Actor is null || revision.ChangedAt == default ||
            !ValidText(revision.Actor.Kind, 200) || !ValidText(revision.Actor.Id, 200) ||
            !ValidText(revision.Actor.Display, 200))
            return false;
        var normalized = TechnologyInventoryRules.Normalize(revision.Content);
        return normalized == revision.Content && TechnologyInventoryRules.Validate(normalized) is null;
    }

    static bool IsValidDirectoryView(DataFlowView view, Uuid tenantId, Uuid flowId) =>
        view.TenantId == tenantId && view.DataFlowId == flowId && view.Revision >= 1 &&
        view.SourceKind == TechnologyInventoryStreams.ManualSource && view.Content is not null &&
        TechnologyInventoryRules.Normalize(view.Content) == view.Content &&
        TechnologyInventoryRules.Validate(view.Content) is null && view.LastChangedAt != default &&
        view.LastChangedBy is { } actor && ValidText(actor.Kind, 200) &&
        ValidText(actor.Id, 200) && ValidText(actor.Display, 200);

    static bool MatchesCurrent(DataFlowView current, DataFlow aggregate,
        IReadOnlyList<DataFlowRevisionRecorded> history)
    {
        var latest = history[^1];
        return aggregate.Content == latest.Content && current.Revision == latest.Revision &&
               current.Content == latest.Content && current.LastChangedBy == latest.Actor &&
               current.LastChangedAt == latest.ChangedAt;
    }

    static void ValidateReferences(IReadOnlyList<TypeIBaselineSourceReference> sources,
        Uuid flowId, DataFlowContent content, List<string> blockers, List<string> differences)
    {
        var references = new List<(string Kind, Uuid Id, string Description)>
        {
            content.SourceType switch
            {
                TechnologyInventoryRules.ComponentReference => ("system_component_revision",
                    content.SourceId, "source component"),
                TechnologyInventoryRules.SystemInstanceReference => ("reviewed_system_revision",
                    content.SourceId, "source system"),
                _ => (string.Empty, Uuid.Empty, "source"),
            },
        };
        if (content.DestinationType == TechnologyInventoryRules.ComponentReference &&
            content.DestinationId is { } destinationId)
            references.Add(("system_component_revision", destinationId, "destination component"));
        references.AddRange(content.InformationAssetIds.Select(assetId =>
            ("information_asset_revision", assetId, "carried information asset")));

        foreach (var reference in references)
        {
            if (reference.Kind.Length == 0 || !HasValidReference(sources,
                    reference.Kind, reference.Id))
            {
                blockers.Add($"A data flow's {reference.Description} was not captured by the Type I baseline.");
                differences.Add($"Data flow {flowId} has an uncaptured {reference.Description}.");
            }
        }
    }

    static bool HasValidReference(IReadOnlyList<TypeIBaselineSourceReference> sources,
        string kind, Uuid recordId)
    {
        var matches = sources.Where(source => source.Kind == kind && source.RecordId == recordId)
            .ToArray();
        var prefix = kind switch
        {
            "system_component_revision" => "technology_component_revision_v1",
            "reviewed_system_revision" => "reviewed_system_revision_v1",
            "information_asset_revision" => "information_asset_revision_v1",
            _ => string.Empty,
        };
        return prefix.Length > 0 && matches.Length == 1 && matches[0].Revision >= 1 &&
               matches[0].VersionId == Uuid.CreateVersion5(recordId,
                   $"{prefix}:{matches[0].Revision}") && IsSha256(matches[0].ContentSha256);
    }

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static Uuid RevisionVersionId(Uuid flowId, long revision) =>
        Uuid.CreateVersion5(flowId, $"data_flow_revision_v1:{revision}");

    static string Digest(Uuid tenantId, Uuid flowId, DataFlowRevisionRecorded revision)
    {
        var content = new DataFlowSourceContent(tenantId, flowId, revision.Revision,
            revision.Content, revision.Actor, revision.ChangedAt);
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.DataFlowSourceContent);
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

    static Result<IReadOnlyList<DataFlowRevisionRecorded>> InvalidHistory() =>
        Result<IReadOnlyList<DataFlowRevisionRecorded>>.Failure(new RequestError(
            RequestErrorKind.Conflict, "The data-flow source history is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while data-flow scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The technology-inventory directory returned invalid data-flow baseline data."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The data-flow source or projection changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            "data_flows", sources, blockers, scopeDifferences));
}
