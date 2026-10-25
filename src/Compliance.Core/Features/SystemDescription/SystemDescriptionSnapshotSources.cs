using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Captures immutable native snapshot evidence. This does not claim a held current-at-approval source permit.</summary>
public sealed class SystemDescriptionSnapshotSources(IDomainEventReader events, IImmutableContentStore content)
{
    public const string Schema = "system-description-snapshot-source-proof.v1";
    public async ValueTask<ImmutableContentRef> CaptureAsync(Uuid tenant, Uuid program, SystemDescriptionScope scope,
        IReadOnlyList<SystemDescriptionSourceReference> sources, EventStreamAddress source, Func<DomainEvent, bool> select,
        CancellationToken ct, (EventStreamAddress Stream, Func<DomainEvent, bool> Select)? additional = null)
    {
        DomainEvent? selected = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(source.Realm, source.Area, source.Resource), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && select(record.Event))
                selected = record.Event;
        if (selected is null || source.Realm != tenant.ToString())
            throw new ImmutableContentUnavailableException("The exact frozen snapshot source fact is not readable yet.");
        var facts = new List<SystemDescriptionSnapshotSourceFact> { new(NativeFactReference.Of(source,selected),
            EventKind.Of(selected),ContentDigest.Sha256(selected,selected.GetType())) };
        if (additional is { } extra)
        {
            DomainEvent? matched = null;
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(extra.Stream.Realm, extra.Stream.Area, extra.Stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
                if (!record.Event.Metadata.IsAudit && extra.Select(record.Event))
                    matched = record.Event;
            if (matched is null || extra.Stream.Realm != tenant.ToString())
                throw new ImmutableContentUnavailableException("The exact approved Type II plan source fact is not readable yet.");
            facts.Add(new(NativeFactReference.Of(extra.Stream, matched), EventKind.Of(matched), ContentDigest.Sha256(matched, matched.GetType())));
        }
        var proof = new SystemDescriptionSnapshotSourceProof(scope, sources, facts);
        var reference = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), Schema, proof, ct).ConfigureAwait(false);
        await VerifyAsync(tenant, program, scope, sources, reference, ct).ConfigureAwait(false);
        return reference;
    }
    public async ValueTask VerifyAsync(Uuid tenant, Uuid program, SystemDescriptionScope scope,
        IReadOnlyList<SystemDescriptionSourceReference> sources, ImmutableContentRef reference, CancellationToken ct)
    {
        if (reference.Schema != Schema)
            throw new ImmutableContentIntegrityException("The description snapshot proof has another schema.");
        var proof = await content.ReadJsonAsync<SystemDescriptionSnapshotSourceProof>(tenant.ToString(), reference, ct).ConfigureAwait(false);
        if (proof.Scope != scope || !Same(proof.Sources, sources) || proof.Facts.Count != (scope.Stage == "type_ii" ? 2 : 1))
            throw new ImmutableContentIntegrityException("The description snapshot proof differs from its exact scope and complete source set.");
        var proven = new List<DomainEvent>();
        foreach (var claimed in proof.Facts)
        {
            if (claimed.Fact.Stream.Realm != tenant.ToString() || claimed.Fact.EventId == Uuid.Empty || claimed.Fact.AggregateVersion == 0)
                throw new ImmutableContentIntegrityException("The description source proof names another tenant.");
            DomainEvent? resolvedFact = null;
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(claimed.Fact.Stream.Realm,
                claimed.Fact.Stream.Area, claimed.Fact.Stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
                if (record.Event.Metadata.EventId == claimed.Fact.EventId && record.Event.Metadata.AggregateVersion == claimed.Fact.AggregateVersion)
                {
                    if (record.Stream != claimed.Fact.Stream)
                        throw new ImmutableContentIntegrityException("The description source resolver returned another exact native stream.");
                    resolvedFact = record.Event;
                }
            if (resolvedFact is null)
                throw new ImmutableContentUnavailableException("The proven description snapshot source is not readable yet.");
            if (EventKind.Of(resolvedFact) != claimed.EventKind || ContentDigest.Sha256(resolvedFact, resolvedFact.GetType()) != claimed.PayloadSha256)
                throw new ImmutableContentIntegrityException("The description snapshot proof differs from its authoritative native payload.");
            proven.Add(resolvedFact);
        }
        var found = proven[0];
        IReadOnlyList<SystemDescriptionSourceReference>? selected = null;
        switch (found)
        {
            case TypeIBaselineFrozen frozen when scope.Stage == "type_i" &&
                proof.Facts[0].Fact.Stream == new ExaminationEngagement(tenant, program, scope.ExaminationId).Stream:
                selected = Baseline((await ExaminationEngagementContentReader.ReadAsync(content, frozen, ct).ConfigureAwait(false)).Baseline!, tenant, program, scope);
                break;
            case TypeIBaselineAmended amended when scope.Stage == "type_i" &&
                proof.Facts[0].Fact.Stream == new ExaminationEngagement(tenant, program, scope.ExaminationId).Stream:
                selected = Baseline((await ExaminationEngagementContentReader.ReadAsync(content, amended, ct).ConfigureAwait(false)).Baseline!, tenant, program, scope);
                break;
            case ITypeIIPeriodStateFact period when scope.Stage == "type_ii" &&
                period.TenantId == tenant && period.Header.ProgramId == program && period.SubjectId == scope.TypeIIPeriodId &&
                period.Header.ExaminationId == scope.ExaminationId && scope.TypeIIPeriodId is { } periodId &&
                proof.Facts[0].Fact.Stream == new TypeIIPeriod(tenant, program, periodId).Stream:
                var state = await content.ReadJsonAsync<TypeIIPeriodView>(tenant.ToString(), period.CurrentContent, ct).ConfigureAwait(false);
                if (period.CurrentContent.Schema != TypeIIPeriodContent.StateSchema || TypeIIPeriodContent.Header(state) != period.Header)
                    throw new ImmutableContentIntegrityException("The proven native period source differs from its verified immutable authority.");
                if (proven[1] is not TypeIIPlanApproved plan || plan.TenantId != tenant || plan.State.ProgramId != program ||
                    plan.SubjectId != state.TypeIIPlanId || proof.Facts[1].Fact.Stream != new TypeIIPlan(tenant, program, state.TypeIIPlanId).Stream || plan.State.CurrentVersionNumber != state.TypeIIPlanVersion ||
                    plan.State.Current is not { } version || version.ContentSha256 != state.TypeIIPlanContentSha256 ||
                    version.Content.ExaminationId != scope.ExaminationId || version.Content.TypeIBaselineId != scope.TypeIBaselineId)
                    throw new ImmutableContentIntegrityException("The Type II description scope differs from its exact approved plan source.");
                var close = state.CloseSnapshots.FirstOrDefault(item => item.CloseSnapshotId == scope.CloseSnapshotId);
                selected = close?.SourceReferences.Select(item => new SystemDescriptionSourceReference(item.SourceType, item.RecordId,
                    item.VersionId, item.Version, item.ContentSha256, "current")).ToArray();
                break;
        }
        if (selected is null || !Same(selected, sources))
            throw new ImmutableContentIntegrityException("The native source does not prove this exact description scope and source-version population.");
    }
    static SystemDescriptionSourceReference[]? Baseline(TypeIBaselineView baseline, Uuid tenant, Uuid program, SystemDescriptionScope scope) =>
        baseline.TenantId != tenant || baseline.ProgramId != program || baseline.ExaminationEngagementId != scope.ExaminationId ||
        baseline.TypeIBaselineId != scope.TypeIBaselineId ? null : baseline.Sources.Select(item => new SystemDescriptionSourceReference(
            item.Kind, item.RecordId, item.VersionId, item.Revision, item.ContentSha256, "current")).ToArray();
    static bool Same(IReadOnlyList<SystemDescriptionSourceReference> left, IReadOnlyList<SystemDescriptionSourceReference> right) =>
        left.Count == right.Count && left.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ThenBy(item => item.VersionId.ToString(), StringComparer.Ordinal).ThenBy(item => item.Revision)
            .SequenceEqual(right.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ThenBy(item => item.VersionId.ToString(), StringComparer.Ordinal).ThenBy(item => item.Revision));
}
