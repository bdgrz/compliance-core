using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Projections;
using Bdgrz.Compliance.Features.AuditPopulations.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AuditPopulations.Aggregates;
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPopulations.History;

/// <summary>Verifies complete actual physical/native history and immutable decision/source bodies outside the command owner.</summary>
static class AuditPopulationHistory
{
    public static async Task<AuditPopulationReadSnapshot> ReadAsync(IDomainEventReader events, IImmutableContentStore content,
        ISourceFactResolver resolver, AuditPopulation owner, CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var rules = new AuditPopulationRules(owner.TenantId, owner.ProgramId, owner.Id);
        var sources = new AuditPopulationSources(events, resolver, content);
        var records = owner.CommittedStreamPosition == 0 ? Array.Empty<DomainEventRecord>() :
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
                if (fact is AuditPopulationDefinedV4 defined)
                {
                    await ApplyDefinitionAsync(owner, rules, sources, content, state, defined, ids, ct).ConfigureAwait(false);
                }
                else if (fact is IAuditPopulationNativeFact native)
                {
                    var before = rules.Current ?? throw Invalid("A native mutation has no exact existing population predecessor.");
                    if (native.Operation is null || native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
                        !ids.Add(native.Operation.LogicalId) || native.FirstResponse is null || native.OperationResult?.Schema != "mutation-result-content.v1" ||
                        native.Command is null || native.Command.Schema != (fact is AuditPopulationGeneratedV4
                            ? AuditPopulationContent.GenerateCommandSchema : AuditPopulationCommandContent.Schema) ||
                        !NativeOwnerHistory.IsOwnRoot(owner, native.Operation, AuditPopulationContent.CommandKind(fact),
                            fact is AuditPopulationRowClassifiedV3 or AuditPopulationGeneratedV4 ? null : (ulong)rules.Revision))
                        throw Invalid("The native population decision differs from its exact root, kind, revision or original result.");
                    IRequest authored;
                    AuditPopulationCommandSelection selection;
                    if (fact is AuditPopulationGeneratedV4)
                    {
                        var generate = await NativeContent.ReadAsync<GenerateAuditPopulation>(content, owner.TenantId,
                            native.Command, ct).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(generate.GenerationKey) || generate.GenerationKey.Trim().Length > 200)
                            throw Invalid("The original population generation key is invalid.");
                        authored = generate;
                        selection = new AuditPopulationCommandSelection(Uuid.CreateVersion5(generate.DefinitionId, generate.GenerationKey),
                            generate.TenantId, generate.ProgramId, generate.DefinitionId, null, nameof(GenerateAuditPopulation),
                            AuthoredIntentDigest.Compute(generate), new(null, null, null, null, null, null));
                    }
                    else
                    {
                        var command = await NativeContent.ReadAsync<AuditPopulationCommandContent>(content, owner.TenantId, native.Command, ct).ConfigureAwait(false);
                        authored = AuditPopulationContent.Authored(command);
                        selection = AuditPopulationCommandSelection.Of(authored);
                    }
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
                    if (fact is AuditPopulationGeneratedV4 && native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId)
                        await VerifyUnitReceiptAsync(content, owner.TenantId, native.FirstResponse,
                            native.OperationResult!, ct).ConfigureAwait(false);
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
    static async Task ApplyDefinitionAsync(AuditPopulation owner, AuditPopulationRules rules,
        AuditPopulationSources sources, IImmutableContentStore content, AuditPopulationView state,
        AuditPopulationDefinedV4 fact, HashSet<Uuid> ids, CancellationToken ct)
    {
        if (rules.Current is not null || fact.Operation is null || fact.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
            !ids.Add(fact.Operation.LogicalId) ||
            fact.Operation.LogicalId != owner.Id || fact.FirstResponse is null ||
            fact.OperationResult?.Schema != "mutation-result-content.v1" || fact.Command is null ||
            fact.Command.Schema != AuditPopulationContent.DefineCommandSchema ||
            !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(DefineAuditPopulation), null))
            throw Invalid("The definition differs from its exact original creation operation.");
        var command = await NativeContent.ReadAsync<DefineAuditPopulation>(content, owner.TenantId, fact.Command, ct).ConfigureAwait(false);
        var actor = ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay);
        if (command.TenantId != owner.TenantId || command.ProgramId != owner.ProgramId || command.DefinitionId != owner.Id ||
            command.PeriodId != state.TypeIIPeriodId || command.PeriodCloseId != state.CloseSnapshot.CloseSnapshotId ||
            AuthoredIntentDigest.Compute(command) != fact.Operation.AuthoredIntentSha256 || fact.Operation.Actor != actor ||
            fact.Operation.AcceptedAt != state.UpdatedAt)
            throw Invalid("The definition differs from its complete authored scope, actor or acceptance time.");
        var period = await sources.VerifyDefinitionAsync(owner, state, fact.SourceProofs, ct).ConfigureAwait(false);
        var failure = rules.Define(command.DefinitionId, command.Definition, period,
            state.ActorMemberId, state.ActorDisplay, fact.Operation.AcceptedAt);
        if (failure is not null || rules.PendingChanges.Count != 1 || rules.Current is not { } decided ||
            ContentDigest.OfJson(decided) != fact.CurrentContent.Sha256)
            throw Invalid("The population definition does not follow its exact frozen source and authored decision.");
        if (fact.Operation.LogicalId == owner.Acceptance.SelectedLogicalId)
        {
            if (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(fact) || selected.Metadata != fact.Metadata)
                throw Invalid("The selected definition receipt differs from its actual payload or metadata.");
            await VerifyUnitReceiptAsync(content, owner.TenantId, fact.FirstResponse, fact.OperationResult!, ct).ConfigureAwait(false);
        }
        rules.ClearPendingChanges();
    }
    internal static async Task VerifyUnitReceiptAsync(IImmutableContentStore content, Uuid tenantId,
        Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef first,
        Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef resultReference, CancellationToken ct)
    {
        var response = await FirstResponses.ReplayAsync(content, tenantId.ToString(), first, ct).ConfigureAwait(false);
        var result = await NativeContent.ReadAsync<MutationResultContent>(content, tenantId, resultReference, ct).ConfigureAwait(false);
        if (response.HttpStatus != 200 || response.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
            response.ResultSchema != UnitResult.Schema || response.Body is not StandardResponseBody body ||
            body.Content.Schema != UnitResult.Schema || result.Schema != UnitResult.Schema || result.Value != response.Body)
            throw Invalid("The selected population receipt is not its exact original Unit response.");
        _ = await NativeContent.ReadAsync<UnitResult>(content, tenantId, body.Content, ct).ConfigureAwait(false);
    }
    static void Apply(AuditPopulationRules rules, DomainEvent fact)
    {
        try
        { rules.Apply(fact); }
        catch (InvalidOperationException error) when (error.Message is "The audit-population event is invalid." or "The population read mutation has another exact kind.")
        { throw Invalid(error.Message); }
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
