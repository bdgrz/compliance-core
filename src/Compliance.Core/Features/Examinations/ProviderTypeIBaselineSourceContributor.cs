using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures scoped provider and due-diligence history at the Type I cutoff.</summary>
public sealed class ProviderTypeIBaselineSourceContributor(
    IProviderReader providers, IAssuranceReader assurance, IAggregateReader reader,
    IDomainEventReader events, IProviderCsocBaselineReader providerCsocs, IImmutableContentStore contentStore)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const int MaximumEventsPerStream = 100_000;
    const string DigestDomain = "bdgrz.snapshot.source.provider.v1\0";

    public string Family => "providers";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty || engagement.ProgramId == Uuid.Empty ||
            engagement.PeriodStart == default)
            return Failure(RequestErrorKind.Validation,
                "A provider baseline read requires its engagement and source context.");

        var providerFence = await providers.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        var assuranceFence = await assurance.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        var providerPattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(),
            Provider.Area);
        var assurancePattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(),
            ProviderAssuranceRegister.Area);
        if (await HasPendingSourceAsync(providerPattern, providerFence, ct).ConfigureAwait(false) ||
            await HasPendingSourceAsync(assurancePattern, assuranceFence, ct).ConfigureAwait(false))
            return BehindSource();

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var listed = await ListProvidersAsync(engagement.TenantId, ct).ConfigureAwait(false);
        if (!listed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(listed.Error);
        var providerHistories = await ReadProviderHistoryAsync(engagement.TenantId,
            engagement.ProgramId, through, listed.Value, ct).ConfigureAwait(false);
        if (!providerHistories.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(providerHistories.Error);
        var assuranceHistories = await ReadAssuranceHistoryAsync(engagement.TenantId, through,
            providerHistories.Value, ct).ConfigureAwait(false);
        if (!assuranceHistories.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(assuranceHistories.Error);

        IReadOnlySet<Uuid> effectiveCsocProviders = new HashSet<Uuid>();
        if (providerHistories.Value.Values.Any(history => history.AsOf is { } provider &&
                IsInScope(provider.Content, engagement.ProgramId, through) &&
                provider.Content.Subservice && provider.Content.BoundaryTreatment == "carve_out"))
        {
            var csocRead = await providerCsocs.ReadEffectiveProviderCsocsAsync(engagement,
                context, ct).ConfigureAwait(false);
            if (!csocRead.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(csocRead.Error);
            if (csocRead.Value is null || csocRead.Value.Contains(Uuid.Empty))
                return InvalidDirectory<TypeIBaselineSourceContribution>();
            effectiveCsocProviders = csocRead.Value;
        }

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var history in providerHistories.Value.Values.OrderBy(
                     static history => history.ProviderId.ToString(), StringComparer.Ordinal))
        {
            if (history.AsOf is not { } provider || history.AsOfRevision == 0)
                continue;
            if (!IsInScope(provider.Content, engagement.ProgramId, through))
                continue;

            var assuranceHistory = assuranceHistories.Value.GetValueOrDefault(
                history.ProviderId) ?? new ProviderAssuranceHistory();
            var sourceEvents = history.AsOfEvents.Concat(assuranceHistory.AsOf.Events)
                .OrderBy(static item => item.Area, StringComparer.Ordinal)
                .ThenBy(static item => item.AggregateId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.AggregateVersion).ToArray();
            var content = new ProviderSourceContent(engagement.PeriodStart,
                engagement.ProgramId, Array.AsReadOnly(sourceEvents));
            try
            {
                sources.Add(new TypeIBaselineSourceReference("provider", history.ProviderId,
                    Uuid.CreateVersion5(history.ProviderId, "provider_v1:" +
                        engagement.PeriodStart.ToString("yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture)),
                    history.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                var difference = $"Provider {provider.Content.Name} has invalid canonical source content.";
                blockers.Add(difference);
                scopeDifferences.Add(difference);
                continue;
            }

            AddReadinessBlockers(provider, assuranceHistory.AsOf, engagement.PeriodStart,
                effectiveCsocProviders.Contains(history.ProviderId), blockers, scopeDifferences);
        }

        if (!await ConfirmFenceAsync(engagement.TenantId, providerPattern, providerFence,
                providers.LoadCheckpointAsync, ct).ConfigureAwait(false) ||
            !await ConfirmFenceAsync(engagement.TenantId, assurancePattern, assuranceFence,
                assurance.LoadCheckpointAsync, ct).ConfigureAwait(false))
            return BehindSource();

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(), scopeDifferences.AsReadOnly()));
    }

    async ValueTask<Result<IReadOnlyList<ProviderView>>> ListProvidersAsync(Uuid tenantId,
        CancellationToken ct)
    {
        var values = new List<ProviderView>();
        var ids = new HashSet<Uuid>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await providers.ListAsync(tenantId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory<IReadOnlyList<ProviderView>>();
            foreach (var item in page.Items)
            {
                if (!ValidProviderView(item, tenantId) || !ids.Add(item.ProviderId))
                    return InvalidDirectory<IReadOnlyList<ProviderView>>();
                values.Add(item);
            }
            var next = page.NextCursor;
            if (next is not null && (next == cursor || !cursors.Add(next)))
                return InvalidDirectory<IReadOnlyList<ProviderView>>();
            cursor = next;
        } while (cursor is not null);
        return Result<IReadOnlyList<ProviderView>>.Success(values.AsReadOnly());
    }

    async ValueTask<Result<Dictionary<Uuid, ProviderHistory>>> ReadProviderHistoryAsync(
        Uuid tenantId, Uuid programId, DateTimeOffset through,
        IReadOnlyList<ProviderView> summaries, CancellationToken ct)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), Provider.Area);
        var histories = new Dictionary<Uuid, ProviderHistory>();
        var eventIds = new HashSet<Uuid>();
        var requestIds = new HashSet<Uuid>();
        var versions = new Dictionary<EventStreamAddress, ulong>();
        var priorAt = new Dictionary<EventStreamAddress, DateTimeOffset>();
        var count = 0;
        await foreach (var record in events.ReadAsync(pattern, ct).ConfigureAwait(false))
        {
            if (++count > MaximumEventsPerStream || record.Stream.Realm != tenantId.ToString() ||
                record.Stream.Area != Provider.Area ||
                !Uuid.TryParse(record.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture,
                    out var providerId) ||
                record.Event?.Metadata is not { } metadata || metadata.EventId == Uuid.Empty ||
                !eventIds.Add(metadata.EventId) || metadata.AggregateId != providerId ||
                metadata.AggregateVersion != versions.GetValueOrDefault(record.Stream) + 1 ||
                metadata.OccurredOn == default ||
                priorAt.TryGetValue(record.Stream, out var prior) && metadata.OccurredOn < prior)
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();
            versions[record.Stream] = metadata.AggregateVersion;
            priorAt[record.Stream] = metadata.OccurredOn;
            if (record.Event is ProviderChangeAccepted or ProviderChangeCanceled)
                continue;
            if (!TryProviderEvent(record.Event, tenantId, providerId, out var requestId, out var revision,
                    out var contentRef, out var actor, out var recordedAt))
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();
            ProviderContent providerContent;
            try
            {
                var bytes = await contentStore.ReadVerifiedAsync(tenantId.ToString(), contentRef, ct)
                    .ConfigureAwait(false);
                providerContent = JsonSerializer.Deserialize(bytes.Span,
                    ComplianceCoreJsonContext.Default.ProviderContent)!;
            }
            catch (Exception exception) when (exception is ImmutableContentIntegrityException or JsonException
                or NullReferenceException)
            {
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();
            }

            if (!requestIds.Add(requestId) || recordedAt == default ||
                !ValidProviderContent(providerContent) || !ValidMemberActor(actor))
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();

            if (!histories.TryGetValue(providerId, out var history))
            {
                history = new ProviderHistory(providerId);
                histories.Add(providerId, history);
            }
            if (revision != history.CurrentRevision + 1 ||
                revision == 1 && record.Event is not ProviderRecorded ||
                revision > 1 && record.Event is not ProviderRevised)
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();

            var view = ProviderViewFor(tenantId, providerId, revision, providerContent,
                actor!, recordedAt);
            if (histories.Values.Any(other => other.ProviderId != providerId &&
                    other.Current is { } existing && StringComparer.OrdinalIgnoreCase.Equals(
                        existing.Content.Name, view.Content.Name)))
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();

            var sourceEvent = SerializeEvent(record.Event, Provider.Area, providerId, metadata, providerContent);
            var isAsOf = recordedAt <= through && metadata.OccurredOn <= through;
            if (!history.Apply(view, sourceEvent, isAsOf))
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();
        }

        var summaryById = summaries.ToDictionary(static summary => summary.ProviderId);
        if (summaryById.Count != histories.Count || histories.Keys.Any(id =>
                !summaryById.ContainsKey(id)))
            return InvalidDirectory<Dictionary<Uuid, ProviderHistory>>();
        var currentSource = new ProviderSource(reader, contentStore);
        foreach (var history in histories.Values)
        {
            var current = await currentSource.GetAsync(tenantId, history.ProviderId, ct).ConfigureAwait(false);
            if (current is null || history.Current is null ||
                !SameJson(current, history.Current, ComplianceCoreJsonContext.Default.ProviderView) ||
                !SameJson(summaryById[history.ProviderId], current,
                    ComplianceCoreJsonContext.Default.ProviderView))
                return InvalidHistory<Dictionary<Uuid, ProviderHistory>>();
        }
        return Result<Dictionary<Uuid, ProviderHistory>>.Success(histories);
    }

    async ValueTask<Result<Dictionary<Uuid, ProviderAssuranceHistory>>> ReadAssuranceHistoryAsync(
        Uuid tenantId, DateTimeOffset through,
        IReadOnlyDictionary<Uuid, ProviderHistory> providerHistories, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), ProviderAssuranceRegister.Area,
            tenantId.ToString());
        var histories = new Dictionary<Uuid, ProviderAssuranceHistory>();
        var eventIds = new HashSet<Uuid>();
        var requestIds = new HashSet<Uuid>();
        var expectedVersion = 1UL;
        DateTimeOffset? priorRecordedAt = null;
        var excluded = false;
        var count = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (++count > MaximumEventsPerStream || record.Stream != stream ||
                record.Event?.Metadata is not { } metadata || metadata.EventId == Uuid.Empty ||
                !eventIds.Add(metadata.EventId) || metadata.AggregateId != tenantId ||
                metadata.AggregateVersion != expectedVersion || metadata.OccurredOn == default ||
                priorRecordedAt is { } prior && metadata.OccurredOn < prior ||
                !TryAssuranceEvent(record.Event, tenantId, out var providerId,
                    out var requestId, out var recordedAt) || recordedAt == default ||
                !requestIds.Add(requestId) || !providerHistories.TryGetValue(providerId,
                    out var providerHistory))
                return InvalidHistory<Dictionary<Uuid, ProviderAssuranceHistory>>();

            var providerRevision = ProviderRevisionFor(record.Event!);
            var priorAssurance = histories.GetValueOrDefault(providerId);
            if (providerRevision == 0 && priorAssurance is not null &&
                record.Event is ProviderCoverageGapClosed closed)
                providerRevision = priorAssurance.Current.ProviderRevisionForGap(closed.GapId);
            else if (providerRevision == 0 && priorAssurance is not null &&
                     record.Event is ProviderCoverageGapRiskAcceptanceLinked linked)
                providerRevision = priorAssurance.Current.ProviderRevisionForGap(linked.GapId);
            var observed = providerHistory!.GetRevision(providerRevision);
            if (observed is null || observed.RecordedAt > recordedAt ||
                !TryAssuranceActor(record.Event!, out var actor) || !ValidMemberActor(actor))
                return InvalidHistory<Dictionary<Uuid, ProviderAssuranceHistory>>();

            if (!histories.TryGetValue(providerId, out var history))
            {
                history = new ProviderAssuranceHistory();
                histories.Add(providerId, history);
            }
            if (!history.Current.Apply(record.Event!, observed))
                return InvalidHistory<Dictionary<Uuid, ProviderAssuranceHistory>>();

            var isAsOf = recordedAt <= through && metadata.OccurredOn <= through;
            if (isAsOf)
            {
                if (excluded || !history.AsOf.Apply(record.Event!, observed))
                    return InvalidHistory<Dictionary<Uuid, ProviderAssuranceHistory>>();
                history.AsOf.Events.Add(SerializeEvent(record.Event!,
                    ProviderAssuranceRegister.Area, tenantId, metadata));
            }
            else
                excluded = true;

            priorRecordedAt = metadata.OccurredOn;
            expectedVersion++;
        }

        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(tenantId), ct)
            .ConfigureAwait(false);
        foreach (var providerId in providerHistories.Keys.OrderBy(static id => id.ToString(),
                     StringComparer.Ordinal))
        {
            var history = histories.GetValueOrDefault(providerId) ??
                new ProviderAssuranceHistory();
            if (!SameJsonSequence(source.Reports(providerId), history.Current.Reports,
                    ComplianceCoreJsonContext.Default.AssuranceReportView,
                    static item => item.ReportId) ||
                !SameJsonSequence(source.Reviews(providerId), history.Current.Reviews,
                    ComplianceCoreJsonContext.Default.ProviderReviewView,
                    static item => item.ReviewId) ||
                !SameJsonSequence(source.CoverageGaps(providerId), history.Current.CoverageGaps,
                    ComplianceCoreJsonContext.Default.ProviderCoverageGapView,
                    static item => item.GapId))
                return InvalidHistory<Dictionary<Uuid, ProviderAssuranceHistory>>();

            var reports = await ListAllAsync((limit, cursor, token) =>
                    assurance.ListReportsAsync(tenantId, providerId, limit, cursor, token),
                item => item.TenantId == tenantId && item.ReportId != Uuid.Empty &&
                    item.ProviderId == providerId && item.Revision > 0 && !item.Redacted,
                static item => item.ReportId, ct).ConfigureAwait(false);
            var reviews = await ListAllAsync((limit, cursor, token) =>
                    assurance.ListReviewsAsync(tenantId, providerId, limit, cursor, token),
                item => item.TenantId == tenantId && item.ReviewId != Uuid.Empty &&
                    item.ProviderId == providerId && !item.Redacted,
                static item => item.ReviewId, ct).ConfigureAwait(false);
            var gaps = await ListAllAsync((limit, cursor, token) =>
                    assurance.ListCoverageGapsAsync(tenantId, providerId, limit, cursor, token),
                item => item.TenantId == tenantId && item.GapId != Uuid.Empty &&
                    item.ProviderId == providerId && !item.Redacted,
                static item => item.GapId, ct).ConfigureAwait(false);
            if (!reports.IsSuccess || !reviews.IsSuccess || !gaps.IsSuccess ||
                !SameJsonSequence(reports.Value, history.Current.Reports,
                    ComplianceCoreJsonContext.Default.AssuranceReportView,
                    static item => item.ReportId) ||
                !SameJsonSequence(reviews.Value, history.Current.Reviews,
                    ComplianceCoreJsonContext.Default.ProviderReviewView,
                    static item => item.ReviewId) ||
                !SameJsonSequence(gaps.Value, history.Current.CoverageGaps,
                    ComplianceCoreJsonContext.Default.ProviderCoverageGapView,
                    static item => item.GapId))
                return InvalidDirectory<Dictionary<Uuid, ProviderAssuranceHistory>>();

            foreach (var reportRevision in history.Current.ReportRevisions)
            {
                var retained = await assurance.GetReportRevisionAsync(tenantId,
                    reportRevision.ReportId, reportRevision.Revision, ct).ConfigureAwait(false);
                if (retained is null || !SameJson(retained, reportRevision,
                        ComplianceCoreJsonContext.Default.AssuranceReportView))
                    return InvalidDirectory<Dictionary<Uuid, ProviderAssuranceHistory>>();
            }
            foreach (var gap in history.Current.CoverageGaps)
            {
                var retained = await assurance.GetCoverageGapAsync(tenantId, gap.GapId, ct)
                    .ConfigureAwait(false);
                if (retained is null || !SameJson(retained, gap,
                        ComplianceCoreJsonContext.Default.ProviderCoverageGapView))
                    return InvalidDirectory<Dictionary<Uuid, ProviderAssuranceHistory>>();
            }
        }
        return Result<Dictionary<Uuid, ProviderAssuranceHistory>>.Success(histories);
    }

    static bool TryProviderEvent(DomainEvent domainEvent, Uuid tenantId, Uuid streamProviderId,
        out Uuid requestId, out long revision, out ImmutableContentRef content, out ActorReference? actor,
        out DateTimeOffset recordedAt)
    {
        switch (domainEvent)
        {
            case ProviderRecorded recorded:
                (requestId, revision, content, actor, recordedAt) =
                    (recorded.RequestId, 1, recorded.ContentContent, recorded.Actor, recorded.RecordedAt);
                return recorded.TenantId == tenantId && recorded.ProviderId == streamProviderId;
            case ProviderRevised revised:
                (requestId, revision, content, actor, recordedAt) =
                    (revised.RequestId, revised.Revision, revised.ContentContent, revised.Actor, revised.RecordedAt);
                return revised.TenantId == tenantId && revised.ProviderId == streamProviderId;
            default:
                (requestId, revision, content, actor, recordedAt) = (Uuid.Empty, 0, null!, null, default);
                return false;
        }
    }

    static bool TryAssuranceEvent(DomainEvent domainEvent, Uuid tenantId,
        out Uuid providerId, out Uuid requestId, out DateTimeOffset recordedAt)
    {
        switch (domainEvent)
        {
            case AssuranceReportRecorded ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId, ev.RecordedAt);
                return ev.TenantId == tenantId;
            case AssuranceReportRevised ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId, ev.RecordedAt);
                return ev.TenantId == tenantId;
            case ProviderReviewRecorded ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId, ev.RecordedAt);
                return ev.TenantId == tenantId;
            case ProviderCoverageGapRecorded ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId, ev.RecordedAt);
                return ev.TenantId == tenantId;
            case ProviderCoverageGapClosed ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId, ev.ClosedAt);
                return ev.TenantId == tenantId;
            case ProviderCoverageGapRiskAcceptanceLinked ev:
                (providerId, requestId, recordedAt) = (ev.ProviderId, ev.RequestId,
                    ev.Acceptance.LinkedAt);
                return ev.TenantId == tenantId;
            default:
                (providerId, requestId, recordedAt) = (Uuid.Empty, Uuid.Empty, default);
                return false;
        }
    }

    static long ProviderRevisionFor(DomainEvent domainEvent) => domainEvent switch
    {
        AssuranceReportRecorded ev => ev.ProviderRevision,
        AssuranceReportRevised ev => ev.ProviderRevision,
        ProviderReviewRecorded ev => ev.ProviderRevision,
        ProviderCoverageGapRecorded ev => ev.ProviderRevision,
        _ => 0,
    };

    static bool TryAssuranceActor(DomainEvent domainEvent, out ActorReference? actor)
    {
        actor = domainEvent switch
        {
            AssuranceReportRecorded ev => ev.Actor,
            AssuranceReportRevised ev => ev.Actor,
            ProviderReviewRecorded ev => ev.Actor,
            ProviderCoverageGapRecorded ev => ev.Actor,
            ProviderCoverageGapClosed ev => ev.Actor,
            ProviderCoverageGapRiskAcceptanceLinked ev => ev.Acceptance.LinkedBy,
            _ => null,
        };
        return actor is not null;
    }

    static bool IsInScope(ProviderContent content, Uuid programId, DateTimeOffset through) =>
        content.Dependencies?.Any(dependency => dependency.ProgramId == programId &&
            dependency.EffectiveFrom <= through &&
            (dependency.EffectiveUntilExclusive is null ||
             through < dependency.EffectiveUntilExclusive.Value)) == true;

    static void AddReadinessBlockers(ProviderView provider, ProviderAssuranceState assurance,
        DateOnly asOf, bool hasEffectiveCsoc, List<string> blockers,
        List<string> scopeDifferences)
    {
        var unresolved = ProviderRules.Unresolved(provider.Content)
            .Where(static fact => fact != "csocs").ToArray();
        if (unresolved.Length > 0)
        {
            var difference = $"Provider {provider.Content.Name} has unresolved facts: {string.Join(", ", unresolved)}.";
            blockers.Add(difference);
            if (provider.Content.Dependencies?.Any(static dependency =>
                    dependency.SubjectId is null) == true)
                scopeDifferences.Add(difference);
        }

        if (provider.Content.Subservice && provider.Content.BoundaryTreatment == "carve_out" &&
            !hasEffectiveCsoc)
            blockers.Add($"Carved-out subservice provider {provider.Content.Name} has no effective, applicable CSOC linked to it at the Type I date.");

        if (provider.Content.Materiality == "material")
        {
            var coverage = ProviderAssuranceCoverage.Evaluate(provider, assurance.Reports,
                assurance.Reviews, asOf);
            if (!coverage.Complete)
                blockers.Add($"Material provider {provider.Content.Name} has incomplete current due diligence on the Type I date ({coverage.Status}).");
        }

        foreach (var gap in assurance.CoverageGaps.Where(static gap => gap.Status == "open"))
            blockers.Add($"Provider {provider.Content.Name} has an open coverage gap for {gap.Content.Service}.");
    }

    static bool ValidProviderView(ProviderView? view, Uuid tenantId) => view is not null &&
        view.TenantId == tenantId && view.ProviderId != Uuid.Empty && view.Revision > 0 &&
        view.SourceKind == "manual" && view.Lifecycle == "active" &&
        ValidProviderContent(view.Content) && ValidMemberActor(view.RecordedBy) &&
        view.RecordedAt != default && view.Unresolved is not null &&
        view.Unresolved.SequenceEqual(ProviderRules.Unresolved(view.Content));

    static bool ValidProviderContent(ProviderContent? content)
    {
        if (ProviderRules.InputError(content) is not null)
            return false;
        var normalized = ProviderRules.Normalize(content!);
        return ProviderRules.Validate(normalized) is null && SameJson(content, normalized,
            ComplianceCoreJsonContext.Default.ProviderContent);
    }

    static bool ValidReportContent(AssuranceReportContent? content)
    {
        if (AssuranceRules.InputError(content) is not null)
            return false;
        var normalized = AssuranceRules.Normalize(content!);
        return AssuranceRules.Validate(normalized) is null && SameJson(content, normalized,
            ComplianceCoreJsonContext.Default.AssuranceReportContent);
    }

    static bool ValidReviewContent(ProviderReviewContent? content)
    {
        if (AssuranceRules.InputError(content) is not null)
            return false;
        var normalized = AssuranceRules.Normalize(content!);
        return AssuranceRules.Validate(normalized) is null && SameJson(content, normalized,
            ComplianceCoreJsonContext.Default.ProviderReviewContent);
    }

    static bool ValidMemberActor(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty &&
        !string.IsNullOrWhiteSpace(actor.Display) && actor.Display.Length <= 200 &&
        actor.Display == actor.Display.Trim();

    static bool SameJson<T>(T? left, T? right,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (left is null || right is null)
            return left is null && right is null;
        var leftElement = JsonSerializer.SerializeToElement(left, typeInfo);
        var rightElement = JsonSerializer.SerializeToElement(right, typeInfo);
        using var leftStream = new MemoryStream();
        using var rightStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(leftStream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, leftElement);
        using (var writer = new Utf8JsonWriter(rightStream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, rightElement);
        return leftStream.ToArray().AsSpan().SequenceEqual(rightStream.ToArray());
    }

    static bool SameJsonSequence<T>(IEnumerable<T> left, IEnumerable<T> right,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        Func<T, Uuid> identity)
    {
        var leftValues = left.OrderBy(identity).ToArray();
        var rightValues = right.OrderBy(identity).ToArray();
        return leftValues.Length == rightValues.Length && leftValues.Zip(rightValues)
            .All(pair => identity(pair.First) == identity(pair.Second) &&
                SameJson(pair.First, pair.Second, typeInfo));
    }

    static ProviderSourceEvent SerializeEvent(DomainEvent domainEvent, string area,
        Uuid aggregateId, DomainEventMetadata metadata, ProviderContent? providerContent = null)
    {
        var json = ComplianceCoreJsonContext.Default;
        var (kind, content) = domainEvent switch
        {
            ProviderRecorded ev => ("provider_recorded", WithContent(JsonSerializer.SerializeToElement(ev,
                json.ProviderRecorded), providerContent)),
            ProviderRevised ev => ("provider_revised", WithContent(JsonSerializer.SerializeToElement(ev,
                json.ProviderRevised), providerContent)),
            AssuranceReportRecorded ev => ("assurance_report_recorded",
                JsonSerializer.SerializeToElement(ev, json.AssuranceReportRecorded)),
            AssuranceReportRevised ev => ("assurance_report_revised",
                JsonSerializer.SerializeToElement(ev, json.AssuranceReportRevised)),
            ProviderReviewRecorded ev => ("provider_review_recorded",
                JsonSerializer.SerializeToElement(ev, json.ProviderReviewRecorded)),
            ProviderCoverageGapRecorded ev => ("provider_coverage_gap_recorded",
                JsonSerializer.SerializeToElement(ev, json.ProviderCoverageGapRecorded)),
            ProviderCoverageGapClosed ev => ("provider_coverage_gap_closed",
                JsonSerializer.SerializeToElement(ev, json.ProviderCoverageGapClosed)),
            ProviderCoverageGapRiskAcceptanceLinked ev => ("provider_coverage_gap_risk_acceptance_linked",
                JsonSerializer.SerializeToElement(ev, json.ProviderCoverageGapRiskAcceptanceLinked)),
            _ => throw new JsonException("The provider source stream contains an unknown event."),
        };
        return new ProviderSourceEvent(area, aggregateId, metadata.EventId,
            metadata.AggregateVersion, metadata.OccurredOn, kind, content);
    }

    static string Digest(ProviderSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.ProviderSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    /// <summary>The applied fact together with the complete content it references, so the baseline is self-contained.</summary>
    static JsonElement WithContent(JsonElement fact, ProviderContent? providerContent)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(fact.GetRawText())!.AsObject();
        node["content"] = providerContent is null
            ? null
            : System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(providerContent,
                ComplianceCoreJsonContext.Default.ProviderContent));
        return JsonSerializer.SerializeToElement(node);
    }

    static ProviderView ProviderViewFor(Uuid tenantId, Uuid providerId, long revision,
        ProviderContent content, ActorReference actor, DateTimeOffset recordedAt) =>
        new(tenantId, providerId, revision, content, "manual", "active",
            ProviderRules.Unresolved(content), actor, recordedAt);

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    async ValueTask<bool> ConfirmFenceAsync(Uuid tenantId, EventStreamPattern pattern,
        ProjectionCheckpoint fence,
        Func<Uuid, CancellationToken, ValueTask<ProjectionCheckpoint>> loadCheckpoint,
        CancellationToken ct)
    {
        var current = await loadCheckpoint(tenantId, ct).ConfigureAwait(false);
        return current == fence &&
            !await HasPendingSourceAsync(pattern, current, ct).ConfigureAwait(false);
    }

    static async ValueTask<Result<IReadOnlyList<T>>> ListAllAsync<T>(
        Func<int, string?, CancellationToken, ValueTask<Page<T>>> read,
        Func<T, bool> valid, Func<T, Uuid> identity, CancellationToken ct)
    {
        var items = new List<T>();
        var ids = new HashSet<Uuid>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await read(PageSize, cursor, ct).ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory<IReadOnlyList<T>>();
            foreach (var item in page.Items)
                if (!valid(item) || !ids.Add(identity(item)))
                    return InvalidDirectory<IReadOnlyList<T>>();
                else
                    items.Add(item);
            var next = page.NextCursor;
            if (next is not null && (next == cursor || !cursors.Add(next)))
                return InvalidDirectory<IReadOnlyList<T>>();
            cursor = next;
        } while (cursor is not null);
        return Result<IReadOnlyList<T>>.Success(items.AsReadOnly());
    }

    static Result<T> InvalidDirectory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "A provider directory is invalid or inconsistent."));

    static Result<T> InvalidHistory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "A provider source stream is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BehindSource() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
            RequestErrorKind.Conflict,
            "A provider source projection changed or has not reached its event stream. Retry the query.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Failure(RequestErrorKind kind,
        string message) => Result<TypeIBaselineSourceContribution>.Failure(new RequestError(kind,
        message));

    sealed class ProviderHistory(Uuid providerId)
    {
        readonly List<ProviderRevision> _revisions = [];
        bool _excluded;

        public Uuid ProviderId { get; } = providerId;
        public ProviderView? Current => _revisions.LastOrDefault()?.View;
        public ProviderView? AsOf { get; private set; }
        public long CurrentRevision => Current?.Revision ?? 0;
        public long AsOfRevision => AsOf?.Revision ?? 0;
        public List<ProviderSourceEvent> AsOfEvents { get; } = [];

        public bool Apply(ProviderView view, ProviderSourceEvent sourceEvent, bool isAsOf)
        {
            if (view.Revision != CurrentRevision + 1 || isAsOf && _excluded)
                return false;
            _revisions.Add(new ProviderRevision(view, view.RecordedAt, sourceEvent.RecordedAt));
            if (isAsOf)
            {
                AsOf = view;
                AsOfEvents.Add(sourceEvent);
            }
            else
                _excluded = true;
            return true;
        }

        public ProviderView? GetRevision(long revision) =>
            revision > 0 && revision <= _revisions.Count
                ? _revisions[(int)revision - 1].View
                : null;
    }

    sealed record ProviderRevision(ProviderView View, DateTimeOffset RecordedAt,
        DateTimeOffset MetadataAt);

    sealed class ProviderAssuranceHistory
    {
        public ProviderAssuranceState Current { get; } = new();
        public ProviderAssuranceState AsOf { get; } = new();
    }

    sealed class ProviderAssuranceState
    {
        readonly Dictionary<Uuid, ReportHistory> _reports = [];
        readonly Dictionary<Uuid, ProviderReviewView> _reviews = [];
        readonly Dictionary<Uuid, ProviderCoverageGapView> _coverageGaps = [];

        public List<ProviderSourceEvent> Events { get; } = [];
        public IReadOnlyList<AssuranceReportView> Reports =>
            _reports.Values.Select(static history => history.Current).ToArray();
        public IReadOnlyList<ProviderReviewView> Reviews => _reviews.Values.ToArray();
        public IReadOnlyList<ProviderCoverageGapView> CoverageGaps =>
            _coverageGaps.Values.ToArray();
        public IReadOnlyList<AssuranceReportView> ReportRevisions =>
            _reports.Values.SelectMany(static history => history.Revisions).ToArray();

        public long ProviderRevisionForGap(Uuid gapId) =>
            _coverageGaps.TryGetValue(gapId, out var gap) ? gap.ProviderRevision : 0;

        public bool Apply(DomainEvent domainEvent, ProviderView providerAtRecorded)
        {
            switch (domainEvent)
            {
                case AssuranceReportRecorded ev:
                    if (ev.ReportId == Uuid.Empty || ev.RequestId == Uuid.Empty ||
                        ev.ProviderRevision < 1 || !ValidReportContent(ev.Content) ||
                        !ValidMemberActor(ev.Actor) || _reports.ContainsKey(ev.ReportId))
                        return false;
                    _reports.Add(ev.ReportId, new ReportHistory(MakeReport(ev.TenantId,
                        ev.ReportId, ev.ProviderId, 1, ev.Content, ev.ProviderRevision,
                        ev.Actor, ev.RecordedAt)));
                    return true;
                case AssuranceReportRevised ev:
                    if (ev.ReportId == Uuid.Empty || ev.RequestId == Uuid.Empty ||
                        ev.ProviderRevision < 1 || !ValidReportContent(ev.Content) ||
                        !ValidMemberActor(ev.Actor) || !_reports.TryGetValue(ev.ReportId,
                            out var report) || report.Current.ProviderId != ev.ProviderId ||
                        ev.Revision != report.Current.Revision + 1)
                        return false;
                    report.Add(MakeReport(ev.TenantId, ev.ReportId, ev.ProviderId,
                        ev.Revision, ev.Content, ev.ProviderRevision, ev.Actor,
                        ev.RecordedAt));
                    return true;
                case ProviderReviewRecorded ev:
                    if (ev.ReviewId == Uuid.Empty || ev.RequestId == Uuid.Empty ||
                        ev.ProviderRevision < 1 || !ValidReviewContent(ev.Content) ||
                        !ValidMemberActor(ev.Actor) ||
                        ev.ProviderMateriality is not (null or "material" or "not_material") ||
                        ev.ProviderMateriality != providerAtRecorded.Content.Materiality ||
                        _reviews.ContainsKey(ev.ReviewId))
                        return false;
                    var reportRevision = ev.Content.AssuranceReportId is { } reportId
                        ? _reports.GetValueOrDefault(reportId)?.Current
                        : null;
                    if (ev.Content.AssuranceReportId is not null &&
                            (reportRevision is null || reportRevision.ProviderId != ev.ProviderId ||
                             reportRevision.Content.ReportKind != ev.Content.EvidenceKind ||
                             reportRevision.Revision != ev.AssuranceReportRevision) ||
                        ev.Content.AssuranceReportId is null && ev.AssuranceReportRevision is not null)
                        return false;
                    _reviews.Add(ev.ReviewId, new ProviderReviewView(ev.TenantId,
                        ev.ReviewId, ev.ProviderId, ev.Content, ev.ProviderRevision,
                        ev.AssuranceReportRevision, ev.Actor, ev.RecordedAt));
                    return true;
                case ProviderCoverageGapRecorded ev:
                    if (ev.GapId == Uuid.Empty || ev.RequestId == Uuid.Empty ||
                        ev.ProviderRevision < 1 ||
                        ProviderCoverageGapRules.InputError(ev.Content) is not null ||
                        !ValidMemberActor(ev.Actor) || _coverageGaps.ContainsKey(ev.GapId))
                        return false;
                    _coverageGaps.Add(ev.GapId, new ProviderCoverageGapView(ev.TenantId,
                        ev.GapId, ev.ProviderId, ev.ProviderRevision, 1,
                        ProviderCoverageGapRules.Normalize(ev.Content), "open", ev.Actor,
                        ev.RecordedAt, null, []));
                    return true;
                case ProviderCoverageGapClosed ev:
                    if (ev.RequestId == Uuid.Empty ||
                        ProviderCoverageGapRules.ClosureInputError(ev.Content) is not null ||
                        !ValidMemberActor(ev.Actor) ||
                        !_coverageGaps.TryGetValue(ev.GapId, out var gap) ||
                        gap.ProviderId != ev.ProviderId || gap.Status != "open" ||
                        ev.Revision != gap.Revision + 1 ||
                        ev.Content.SourceKind == gap.Content.SourceKind &&
                        ev.Content.SourceId == gap.Content.SourceId &&
                        ev.Content.SourceRevision <= gap.Content.SourceRevision)
                        return false;
                    _coverageGaps[ev.GapId] = gap with
                    {
                        Revision = ev.Revision,
                        Status = "closed",
                        Closure = new ProviderCoverageGapClosureView(
                            ProviderCoverageGapRules.Normalize(ev.Content), ev.Actor,
                            ev.ClosedAt)
                        { Revision = ev.Revision },
                    };
                    return true;
                case ProviderCoverageGapRiskAcceptanceLinked ev:
                    if (ev.RequestId == Uuid.Empty || ev.Acceptance is null ||
                        ev.Acceptance.ProgramId == Uuid.Empty || ev.Acceptance.RiskId == Uuid.Empty ||
                        ev.Acceptance.AcceptanceId == Uuid.Empty ||
                        ev.Acceptance.ExpiresAt <= ev.Acceptance.LinkedAt ||
                        ev.Acceptance.Revision != ev.Revision ||
                        !ValidMemberActor(ev.Acceptance.LinkedBy) ||
                        !_coverageGaps.TryGetValue(ev.GapId, out var current) ||
                        current.ProviderId != ev.ProviderId || current.Status != "open" ||
                        ev.Revision != current.Revision + 1 ||
                        current.RiskAcceptances.Any(item => item.RiskId == ev.Acceptance.RiskId &&
                            item.AcceptanceId == ev.Acceptance.AcceptanceId) ||
                        current.RiskAcceptances.Count >=
                            ProviderAssuranceRegister.MaximumRiskAcceptanceLinksPerGap)
                        return false;
                    _coverageGaps[ev.GapId] = current with
                    {
                        Revision = ev.Revision,
                        RiskAcceptances = Array.AsReadOnly([.. current.RiskAcceptances,
                            ev.Acceptance]),
                    };
                    return true;
                default:
                    return false;
            }
        }

        static AssuranceReportView MakeReport(Uuid tenantId, Uuid reportId, Uuid providerId,
            long revision, AssuranceReportContent content, long providerRevision,
            ActorReference actor, DateTimeOffset recordedAt) => new(tenantId, reportId,
            providerId, revision, content, providerRevision, content.Exceptions?.Count ?? 0,
            content.ComplementaryControls?.Count ?? 0, content.CoverageGaps?.Count ?? 0,
            actor, recordedAt);

        sealed class ReportHistory(AssuranceReportView first)
        {
            readonly List<AssuranceReportView> _revisions = [first];
            public AssuranceReportView Current => _revisions[^1];
            public IReadOnlyList<AssuranceReportView> Revisions => _revisions;
            public void Add(AssuranceReportView view) => _revisions.Add(view);
        }
    }
}
