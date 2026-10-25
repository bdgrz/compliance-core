using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Authoritative change/incident histories with exact scope, current restricted visibility and receipt cutoff.</summary>
public sealed class TypeIIPeriodCloseHistory(IAggregateReader reader, IDomainEventReader events,
    RestrictedComplianceRecordVisibility visibility, SignificantChangeReader changes, IImmutableContentStore content, INativeEventReceiptReader receipts)
{
    const int MaximumRecords = 100_000;

    public async Task<(IReadOnlyList<TypeIIPeriodCloseNativeSource> Records, IReadOnlyList<Uuid> Unresolved,
        IReadOnlyList<string> Blockers)> ReadAsync(TypeIIPeriodView period, ClaimsPrincipal actor,
        DateTimeOffset recordedThrough, CancellationToken ct)
    {
        var records = new List<TypeIIPeriodCloseNativeSource>();
        var unresolved = new SortedSet<Uuid>();
        var blockers = new List<string>();
        foreach (var area in new[] { "significant-changes", "compliance-incidents" })
        {
            var first = await ReadAreaAsync(period.TenantId, area, period.ProgramId, recordedThrough, ct).ConfigureAwait(false);
            var groups = first.GroupBy(item => item.Fact.Stream.Resource).OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var id = Uuid.Parse(group.Key, System.Globalization.CultureInfo.InvariantCulture);
                if (area == "significant-changes")
                {
                    var latest = (await changes.ReadAsync(period.TenantId, period.ProgramId, id, ct).ConfigureAwait(false)).View
                        ?? throw new ImmutableContentUnavailableException("A discovered native change is not readable yet.");
                    var past = (await changes.ReadThroughAsync(period.TenantId, period.ProgramId, id, recordedThrough, ct).ConfigureAwait(false)).View
                        ?? throw new ImmutableContentIntegrityException("A scoped change history has no complete final immutable state.");
                    if (past.Current is null || past.Current.Content.EffectiveOn > period.PeriodEnd ||
                        past.Current.Content.EffectiveThrough is { } end && end < period.PeriodStart)
                        continue;
                    if (!await visibility.CanReadAsync(period.TenantId, actor, latest.Current!.Content.HandlingClass,
                            latest.Assessments.Select(item => item.AssessedByMemberId).ToArray(), ct).ConfigureAwait(false))
                        throw new CloseSourceAccessException();
                    if (!past.PeriodCloseEligible)
                        blockers.Add($"Significant change {id} lacks its exact required impact/description disposition or review-work links.");
                    records.AddRange(group);
                }
                else
                {
                    var current = await reader.HydrateAsync(new ComplianceIncident(period.TenantId, period.ProgramId, id), ct).ConfigureAwait(false);
                    var latest = current.Current ?? throw new ImmutableContentUnavailableException("A discovered native incident is not readable yet.");
                    var past = IncidentState(group.Last().Payload)
                        ?? throw new ImmutableContentIntegrityException("A scoped incident history has no complete final state.");
                    if (past.Current is null || DateOnly.FromDateTime(past.Current.Content.OccurredAt.UtcDateTime) > period.PeriodEnd ||
                        past.Resolution is { } resolution && DateOnly.FromDateTime(resolution.ResolvedAt.UtcDateTime) < period.PeriodStart)
                        continue;
                    if (!await visibility.CanReadAsync(period.TenantId, actor, latest.Current!.Content.HandlingClass,
                            latest.Assessments.Select(item => item.AssessedByMemberId).ToArray(), ct).ConfigureAwait(false))
                        throw new CloseSourceAccessException();
                    if (!past.PeriodCloseEligible)
                        blockers.Add($"Compliance incident {id} lacks its exact resolved follow-up and description disposition.");
                    if (past.Status != "resolved")
                        unresolved.Add(id);
                    records.AddRange(group);
                }
            }
            var confirmed = await ReadAreaAsync(period.TenantId, area, period.ProgramId, recordedThrough, ct).ConfigureAwait(false);
            if (!Same(first, confirmed))
                throw new ImmutableContentUnavailableException("The native close source population changed while captured.");
        }
        return (records.OrderBy(item => item.Fact.Stream.ToString(), StreamKeyOrder.Instance)
            .ThenBy(item => item.Fact.AggregateVersion).ToArray(), unresolved.ToArray(), blockers);
    }

    async Task<IReadOnlyList<TypeIIPeriodCloseNativeSource>> ReadAreaAsync(Uuid tenant, string area, Uuid program,
        DateTimeOffset through, CancellationToken ct)
    {
        var result = new List<TypeIIPeriodCloseNativeSource>();
        var receiptCut = new RecordedSourceReceiptCutFactory(events, receipts).Create(tenant, through);
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Stream.Realm != tenant.ToString() || record.Stream.Area != area || record.Event.Metadata.EventId == Uuid.Empty ||
                record.Event.Metadata.AggregateVersion == 0)
                throw new ImmutableContentIntegrityException("A close source scan returned a foreign or unbound native record.");
            var state = State(record.Event);
            if (state is null)
                throw new ImmutableContentIntegrityException("A close source scan returned an unrecognized native record.");
            if (state.Value.Program != program)
                continue;
            var receiptStream = await receiptCut.ReadAsync(record.Stream, ct).ConfigureAwait(false);
            if (!receiptStream.Includes(record.Event))
                continue;
            var recordedAt = receiptStream.RecordedAt(record.Event);
            if (state.Value.Tenant != tenant || state.Value.Record.ToString() != record.Stream.Resource)
                throw new ImmutableContentIntegrityException("A close source payload differs from its native tenant or record identity.");
            if (result.Count >= MaximumRecords)
                throw new ImmutableContentIntegrityException("The complete native close source population exceeds the supported capture bound.");
            var captured = Capture(record.Stream, record.Event) with { RecordedAt = recordedAt };
            if (record.Event is ISignificantChangeFact change)
            {
                var body = await content.ReadVerifiedAsync(tenant.ToString(), change.CurrentContent, ct).ConfigureAwait(false);
                var mutation = await content.ReadVerifiedAsync(tenant.ToString(), change.Change, ct).ConfigureAwait(false);
                captured = captured with
                {
                    ContentBlobs = [new(change.CurrentContent,Convert.ToBase64String(body.Span)),
                    new(change.Change,Convert.ToBase64String(mutation.Span))]
                };
            }
            result.Add(captured);
        }
        return result.OrderBy(item => item.Fact.Stream.ToString(), StreamKeyOrder.Instance)
            .ThenBy(item => item.Fact.AggregateVersion).ToArray();
    }

    /// <summary>Retains only the business bodies referenced by this exact selected operating source fact.</summary>
    public async ValueTask<TypeIIPeriodCloseNativeSource> CaptureOperatingAsync(EventStreamAddress stream,
        DomainEvent fact, CancellationToken ct)
    {
        var references = new List<ImmutableContentRef>();
        var tenant = Uuid.Parse(stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        switch (fact)
        {
            case ControlOperatingPlanProposed value:
                references.Add(value.PlanContent);
                break;
            case ControlOperatingPlanApproved value:
                references.Add(value.ReassignmentsManifest);
                break;
            case ControlOccurrenceOpened value:
                references.Add(value.CreationIntent);
                references.Add(value.Intent);
                break;
            case ControlOccurrenceAttested value:
                references.Add(value.AttestationContent);
                break;
            case ControlOccurrenceReviewed value:
                references.Add(value.ReviewContent);
                break;
            case DueControlOccurrencesGenerated value:
                references.Add(value.OccurrenceIdsManifest);
                references.Add(value.CreationIntentManifest);
                references.Add(value.CurrentContent);
                var selection = await content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(tenant.ToString(),
                    value.CreationIntentManifest, ct).ConfigureAwait(false);
                if (selection.Selections is null || selection.Selections.Any(item => item is null || item.Intent is null))
                    throw new ImmutableContentIntegrityException("The selected occurrence generation has no complete immutable creation intent selection.");
                references.AddRange(selection.Selections.Select(item => item.Intent));
                break;
        }
        var blobs = new List<SnapshotSourceContentBlob>();
        foreach (var reference in references.Distinct())
        {
            if (reference is null)
                throw new ImmutableContentIntegrityException("The selected operating source has no exact required business content reference.");
            blobs.Add(new(reference, (await content.ReadVerifiedAsync(tenant.ToString(), reference, ct)
                .ConfigureAwait(false)).ToArray()));
        }
        return Capture(stream, fact) with { PhysicalContentBlobs = blobs };
    }

    public static TypeIIPeriodCloseNativeSource Capture(EventStreamAddress stream, DomainEvent fact) =>
        new(NativeFactReference.Of(stream, fact), EventKind.Of(fact), ContentDigest.Sha256(fact, fact.GetType()),
            JsonSerializer.SerializeToElement(fact, fact.GetType(), ComplianceCoreJsonContext.Default));

    static (Uuid Tenant, Uuid Program, Uuid Record, DateTimeOffset At)? State(DomainEvent fact) => fact switch
    {
        ISignificantChangeFact e => (e.TenantId, e.Header.ProgramId, e.SubjectId, e.Operation.AcceptedAt),
        ComplianceIncidentRecorded e => (e.TenantId, e.State.ProgramId, e.SubjectId, e.OccurredAt),
        ComplianceIncidentRevised e => (e.TenantId, e.State.ProgramId, e.SubjectId, e.OccurredAt),
        ComplianceIncidentImpactAssessed e => (e.TenantId, e.State.ProgramId, e.SubjectId, e.OccurredAt),
        ComplianceIncidentSystemDescriptionDecisionRecorded e => (e.TenantId, e.State.ProgramId, e.SubjectId, e.OccurredAt),
        IncidentResolutionRecorded e => (e.TenantId, e.State.ProgramId, e.SubjectId, e.OccurredAt),
        _ => null,
    };

    static ComplianceIncidentView? IncidentState(JsonElement payload) => payload.TryGetProperty("state", out var state)
        ? JsonSerializer.Deserialize(state, ComplianceCoreJsonContext.Default.ComplianceIncidentView) : null;
    static bool Same(IReadOnlyList<TypeIIPeriodCloseNativeSource> left, IReadOnlyList<TypeIIPeriodCloseNativeSource> right) =>
        left.Count == right.Count && left.Zip(right).All(pair => pair.First.Fact == pair.Second.Fact &&
            pair.First.EventKind == pair.Second.EventKind && pair.First.RecordedAt == pair.Second.RecordedAt && pair.First.PayloadSha256 == pair.Second.PayloadSha256 &&
            AuthoredIntentDigest.Compute(pair.First.ContentBlobs ?? []) == AuthoredIntentDigest.Compute(pair.Second.ContentBlobs ?? []));
}
