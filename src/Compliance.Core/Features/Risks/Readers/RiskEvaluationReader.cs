using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Readers;
/// <summary>Reconstructs exact immutable evaluation history at one hydrated native owner cut.</summary>
public sealed class RiskEvaluationReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<RiskEvaluationReadSnapshot> ReadAsync(Uuid tenant, Uuid risk, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskEvaluation(tenant, risk), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskEvaluationReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskEvaluation(tenant, risk), ct).ConfigureAwait(false);
        return await ReadAsync(owner, program, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskEvaluationRules> RulesAsync(Uuid tenant, Uuid risk, CancellationToken ct = default) =>
        (await ReadAsync(tenant, risk, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<RiskEvaluationRules> RulesAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct = default) =>
        (await ReadAsync(tenant, program, risk, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<RiskEvaluationHistoryEntryView> HistoryEntryAsync(DomainEvent fact, CancellationToken ct = default)
    {
        var native = RiskEvaluationNativeFact.Require(fact, true);
        var body = await RiskEvaluationContent.ReadMutationAsync(content, fact, ct).ConfigureAwait(false);
        if (!RiskEvaluationContent.InScope(body, native.Header))
            throw new ImmutableContentIntegrityException("The evaluation history body has another exact scope.");
        return new(native.TenantId, native.ProgramId, native.RiskId, native.Revision, body switch
        {
            RiskAssessmentRecordedBody => "assessment_recorded",
            RiskTreatmentChosenBody => "treatment_chosen",
            _ => "risk_accepted",
        }, (body as RiskAssessmentRecordedBody)?.Assessment, (body as RiskTreatmentChosenBody)?.Treatment,
            (body as RiskAcceptedBody)?.Acceptance, RiskEvaluationContent.Actor(body), RiskEvaluationContent.At(body));
    }
    internal async ValueTask ValidateFactAsync(DomainEvent value, CancellationToken ct)
    {
        var fact = RiskEvaluationNativeFact.Require(value, true);
        var snapshot = await ReadAsync(fact.TenantId, fact.ProgramId, fact.RiskId, ct).ConfigureAwait(false);
        if (snapshot.Owner.Header is not null && snapshot.ProgramId != fact.ProgramId)
            throw new ImmutableContentIntegrityException("The evaluation projection names another native program scope.");
        var stored = snapshot.Facts.SingleOrDefault(candidate => candidate is IRiskEvaluationFact native &&
            native.Revision == fact.Revision && native.NativeSequence == fact.NativeSequence)
            ?? throw new ImmutableContentUnavailableException("The exact evaluation projection source is not visible yet.");
        if (!RiskEvaluationNativeFact.SameSource(value, stored))
            throw new ImmutableContentIntegrityException("The projected evaluation differs from its exact committed native fact.");
    }
    internal async ValueTask<RiskEvaluationReadSnapshot> ReadAsync(RiskEvaluation owner, Uuid program, CancellationToken ct)
    {
        var rules = new RiskEvaluationRules(owner.TenantId, owner.Id);
        var facts = new List<DomainEvent>();
        if (owner.Header is null || owner.ProgramId != program)
            return new(owner, rules, facts);
        var sequence = 0L;
        var physicalOffset = 0UL;
        ulong? previousMetadataVersion = null;
        var logicalIds = new HashSet<Uuid>();
        var eventIds = new HashSet<Uuid>();
        IRiskEvaluationFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), RiskEvaluation.Area,
            owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record is null || record.Stream != owner.Stream || record.Event is null || record.ResourceOffset != physicalOffset)
                throw new ImmutableContentIntegrityException("The evaluation source carries another committed event stream.");
            var metadata = RiskEvaluationNativeFact.Metadata(record.Event);
            if (metadata.EventId == Uuid.Empty || metadata.AggregateId != owner.Id || !eventIds.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The evaluation source carries another committed event identity.");
            if (metadata.OccurredOn == default || previousMetadataVersion is { } previous &&
                (metadata.AggregateVersion < previous || !metadata.IsAudit && metadata.AggregateVersion == previous))
                throw new ImmutableContentIntegrityException("The evaluation source metadata does not advance independently in physical record order.");
            previousMetadataVersion = metadata.AggregateVersion;
            physicalOffset = checked(physicalOffset + 1);
            if (metadata.IsAudit)
                continue;
            var fact = RiskEvaluationNativeFact.Require(record.Event, true);
            if (record.Stream != owner.Stream || fact.NativeSequence != sequence + 1 || fact.Revision != rules.Revision + 1 ||
                fact.TenantId != owner.TenantId || fact.ProgramId != program || fact.RiskId != owner.Id)
                throw new ImmutableContentIntegrityException("The evaluation history has another native sequence or exact owner scope.");
            if (fact.NativeSequence > owner.NativeSequence)
                break;
            if (!logicalIds.Add(fact.Operation.LogicalId))
                throw new ImmutableContentIntegrityException("An evaluation logical command identity was accepted more than once.");
            var mutation = await RiskEvaluationContent.ReadMutationAsync(content, record.Event, ct).ConfigureAwait(false);
            var before = rules.ToView();
            var after = RiskEvaluationRules.FromView(before);
            after.Apply(mutation);
            var frozen = new FrozenRiskEvaluationMutation(before, after.ToView(), fact.Header, fact.ChangeContent, fact.CurrentAuthorityContent,
                mutation, fact.Operation.CommandKind);
            if (!RiskEvaluationContent.InScope(mutation, fact.Header) || !RiskEvaluationContent.BindingMatches(mutation, fact.Operation) || !RiskEvaluationContent.Verify(frozen) || RiskEvaluationContent.LogicalId(mutation) != fact.Operation.LogicalId ||
                RiskEvaluationContent.Actor(mutation) != fact.Operation.Actor || RiskEvaluationContent.At(mutation) != fact.Operation.AcceptedAt ||
                fact.CurrentAuthorityContent.Schema != RiskEvaluationContent.AuthoritySchema)
                throw new ImmutableContentIntegrityException("The evaluation body differs from its exact native transition and actor.");
            var authority = await content.ReadJsonAsync<RiskEvaluationCurrentAuthority>(owner.TenantId.ToString(), fact.CurrentAuthorityContent, ct).ConfigureAwait(false);
            if (AuthoredIntentDigest.Compute(authority) != AuthoredIntentDigest.Compute(RiskEvaluationContent.Current(after.ToView())))
                throw new ImmutableContentIntegrityException("The evaluation current authority differs from its exact immutable source.");
            rules.Apply(mutation);
            facts.Add(record.Event);
            sequence = fact.NativeSequence;
            last = fact;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact evaluation native source cut is not visible yet.");
        if (last.Header != owner.Header || last.CurrentAuthorityContent != owner.CurrentAuthority)
            throw new ImmutableContentIntegrityException("The evaluation read differs from its hydrated compact authority.");
        rules.ClearPendingChanges();
        rules.SourceNativeSequence = owner.NativeSequence;
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new(owner, rules, facts.AsReadOnly());
    }
}
