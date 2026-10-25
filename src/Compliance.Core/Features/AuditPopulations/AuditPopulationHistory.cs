using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Verifies complete actual physical/native history and immutable decision/source bodies outside the command owner.</summary>
static class AuditPopulationHistory
{
    public static async Task<AuditPopulationReadSnapshot> ReadAsync(IDomainEventReader events, IImmutableContentStore content,
        ISourceFactResolver resolver, AuditPopulation owner, CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var rules = new AuditPopulationRules(owner.TenantId, owner.ProgramId, owner.Id);
        var sources = new AuditPopulationSources(events, resolver, content);
        IReadOnlyList<DomainEventRecord> records = owner.CommittedStreamPosition == 0 ? Array.Empty<DomainEventRecord>() :
            await sources.RecordsAsync(owner.Stream, owner.CommittedStreamPosition, ct).ConfigureAwait(false);
        var positions = records.Where(record => !record.Event.Metadata.IsAudit)
            .ToDictionary(record => record.Event.Metadata.EventId, record => record.ResourceOffset);
        var ids = new HashSet<Uuid>();
        var payload = AuditPopulationReplayCut.Empty;
        var metadata = AuditPopulationReplayCut.Empty;
        var known = AuditPopulationReplayCut.Empty;
        DomainEvent? predecessor = null;
        var compactStarted = false;
        foreach (var fact in facts)
        {
            if (fact is IAuditPopulationStateFact current)
            {
                compactStarted = true;
                if (current.TenantId != owner.TenantId || current.ProgramId != owner.ProgramId || current.SubjectId != owner.Id ||
                    current.CurrentContent is null || current.CurrentContent.Schema != AuditPopulationContent.StateSchema)
                    throw Invalid("The compact population fact has another exact scope or state schema.");
                var state = await NativeContent.ReadAsync<AuditPopulationView>(content, owner.TenantId, current.CurrentContent, ct).ConfigureAwait(false);
                if (AuditPopulationContent.Header(state) != current.Header || ContentDigest.OfJson(state) != current.CurrentContent.Sha256)
                    throw Invalid("The complete population body differs from its compact native authority.");
                if (fact is IAuditPopulationNativeFact native)
                {
                    var before = rules.Current ?? throw Invalid("A native mutation has no exact existing population predecessor.");
                    if (native.Operation is null || native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
                        !ids.Add(native.Operation.LogicalId) || native.FirstResponse is null || native.OperationResult?.Schema != "mutation-result-content.v1" ||
                        native.Command is null || native.Command.Schema != AuditPopulationCommandContent.Schema ||
                        !NativeOwnerHistory.IsOwnRoot(owner, native.Operation, AuditPopulationContent.CommandKind(fact),
                            fact is AuditPopulationRowClassifiedV3 ? null : (ulong)rules.Revision))
                        throw Invalid("The native population decision differs from its exact root, kind, revision or original result.");
                    var command = await NativeContent.ReadAsync<AuditPopulationCommandContent>(content, owner.TenantId, native.Command, ct).ConfigureAwait(false);
                    var authored = AuditPopulationContent.Authored(command);
                    var selection = AuditPopulationCommandSelection.Of(authored);
                    var actor = ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay);
                    if (selection.CommandId != native.Operation.LogicalId || selection.TenantId != owner.TenantId ||
                        selection.ProgramId != owner.ProgramId || selection.PopulationId != owner.Id || selection.Kind != native.Operation.CommandKind ||
                        selection.ExpectedRevision is { } revision && revision != rules.Revision || selection.Digest != native.Operation.AuthoredIntentSha256 ||
                        native.Operation.Actor != actor || native.Operation.AcceptedAt != state.UpdatedAt)
                        throw Invalid("The native population input differs from its complete authored scope, actor, revision or acceptance time.");
                    if (predecessor is null)
                        throw Invalid("The native population proof has no complete actual predecessor.");
                    await sources.VerifyAsync(owner, before, predecessor, positions[fact.Metadata.EventId], payload, metadata,
                        native.SourceProofs, ct).ConfigureAwait(false);
                    var failure = AuditPopulationContent.Decide(rules, authored, actor, native.Operation.AcceptedAt);
                    if (failure is not null || rules.PendingChanges.Count != 1 ||
                        rules.PendingChanges[0].GetType() != AuditPopulationContent.LegacyFact(fact, state).GetType() ||
                        rules.Current is not { } decided || ContentDigest.OfJson(decided) != current.CurrentContent.Sha256)
                        throw Invalid("The native population body does not follow its exact complete reconciliation and authored local decision.");
                    if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                        (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                         !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                        throw Invalid("The selected population receipt differs from its complete actual payload and metadata.");
                    rules.ClearPendingChanges();
                }
                else
                    Apply(rules, AuditPopulationContent.LegacyFact(fact, state));
            }
            else
            {
                if (compactStarted)
                    throw Invalid("An occupied inline population fact cannot be appended after compact content admission.");
                var legacy = AuditPopulationContent.State(fact);
                if (legacy.DefinitionVersions.Count > 64 || legacy.SourceSnapshots.Count > 1000 || legacy.Rows.Count > 10000 ||
                    legacy.Versions.Count > 64 || legacy.Amendments.Count > 64 || legacy.Exports.Count > 256 ||
                    legacy.Reconciliation is { SourceSnapshotIds.Count: > 256 } ||
                    legacy.Versions.Any(version => version is { Reconciliation.SourceSnapshotIds.Count: > 256 }))
                    throw Invalid("The occupied inline population fact exceeds its explicit original replay contract; native bodies are not count capped.");
                Apply(rules, fact);
            }
            predecessor = fact;
            payload = AuditPopulationReplayCut.Append(payload, fact);
            metadata = AuditPopulationReplayCut.Append(metadata, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                known = AuditPopulationReplayCut.Append(known, fact, true);
        }
        if ((rules.Current is { } view ? AuditPopulationContent.Header(view) : null) != owner.Header ||
            owner.CurrentContent is { } last && (rules.Current is null || ContentDigest.OfJson(rules.Current) != last.Sha256))
            throw Invalid("The complete population history differs from its bounded current authority.");
        owner.ReplayCut.Confirm(owner, payload, metadata, known);
        return new(owner, rules, facts);
    }
    static void Apply(AuditPopulationRules rules, DomainEvent fact)
    {
        try { rules.Apply(fact); }
        catch (InvalidOperationException error) when (error.Message is "The audit-population event is invalid." or "The population read mutation has another exact kind.")
        { throw Invalid(error.Message); }
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
