using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.Risks.Projections;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Risks.Readers;

public sealed class RiskDraftReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<RiskDraftReadSnapshot> ReadAsync(Uuid tenant, Uuid risk, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskDraft(tenant, risk), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<RiskDraftRules> RulesAsync(Uuid tenant, Uuid risk, CancellationToken ct = default) =>
        (await ReadAsync(tenant, risk, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<RiskDraftRules> RulesAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct = default) =>
        (await ReadAsync(tenant, program, risk, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<RiskDraftReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new RiskDraft(tenant, risk), ct).ConfigureAwait(false), program, ct).ConfigureAwait(false);
    internal async ValueTask<RiskDraftReadSnapshot> ReadAsync(RiskDraft owner, Uuid program, CancellationToken ct)
    {
        if (owner.Header is null || owner.ProgramId != program)
            return new(owner, new RiskDraftRules(owner.TenantId, owner.Id), null);
        var selected = await content.ReadJsonAsync<RiskDraftRevisionView>(owner.TenantId.ToString(), owner.CurrentContent!, ct).ConfigureAwait(false);
        if (selected is null || owner.CurrentContent!.Schema != RiskDraftFactContent.Schema || RiskDraftFactContent.Header(selected) != owner.Header ||
            RiskDraftRules.Validate(selected.Identifier, selected.Content) is not null ||
            ContentDigest.OfJson(selected.Content) != ContentDigest.OfJson(RiskDraftRules.Clean(selected.Content)))
            throw new ImmutableContentIntegrityException("The risk draft current narrative differs from its native owner cut.");
        return new(owner, RiskDraftRules.FromCurrent(selected, owner.TenantId, owner.Id), selected);
    }
    public async ValueTask ValidateFactAsync(DomainEvent value, CancellationToken ct = default)
    {
        RiskDraftFactContent.ValidateNative(value);
        var candidate = (IRiskDraftFact)value;
        var metadata = RiskDraftFactContent.Metadata(value);
        var stream = candidate.Operation.Owner;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(stream.Realm, stream.Area, stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event.Metadata.EventId != metadata.EventId)
                continue;
            if (record.Stream != stream || record.Event.GetType() != value.GetType() || record.Event is not IRiskDraftFact stored ||
                record.Event.Metadata != metadata || stored.Header != candidate.Header || stored.Content != candidate.Content ||
                stored.NativeSequence != candidate.NativeSequence || stored.Operation != candidate.Operation ||
                stored.FirstResponse != candidate.FirstResponse || stored.OperationResult != candidate.OperationResult)
                throw new ImmutableContentIntegrityException("The risk draft projection source differs from its exact committed native fact.");
            await RiskDraftFactContent.ReadAsync(content, stored, ct).ConfigureAwait(false);
            return;
        }
        throw new ImmutableContentUnavailableException("The exact committed risk draft source is not visible yet.");
    }
    public async ValueTask<IReadOnlyList<RiskDraftRevisionView>> ReadRevisionsAsync(Uuid tenant, Uuid program, Uuid risk, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskDraft(tenant, risk), ct).ConfigureAwait(false);
        if (!owner.IsCreated || owner.ProgramId != program)
            return [];
        var result = new List<RiskDraftRevisionView>();
        var sequence = 0L;
        var position = 0UL;
        var ids = new HashSet<Uuid>();
        IRiskDraftFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area, risk.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var metadata = RiskDraftFactContent.Metadata(record.Event);
            if (metadata.AggregateVersion <= position || !ids.Add(metadata.EventId) || record.Stream != owner.Stream || record.Event is not IRiskDraftFact fact || fact.Header is not { } header ||
                header.TenantId != tenant || header.ProgramId != program || header.RiskId != risk || header.Revision != sequence + 1 ||
                fact.NativeSequence != sequence + 1 || record.Event.Metadata.AggregateId != risk || record.Event.Metadata.EventId == Uuid.Empty ||
                record.Event.Metadata.AggregateVersion == 0)
                throw new ImmutableContentIntegrityException("The risk draft history has another exact native owner sequence.");
            if (fact.NativeSequence > owner.NativeSequence)
                break;
            result.Add(await RiskDraftFactContent.ReadAsync(content, fact, ct).ConfigureAwait(false));
            last = fact;
            sequence = fact.NativeSequence;
            position = metadata.AggregateVersion;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (last is null || sequence != owner.NativeSequence)
            throw new ImmutableContentUnavailableException("The risk draft exact native history cut is not visible yet.");
        if (last.Header != owner.Header || last.Content != owner.CurrentContent)
            throw new ImmutableContentIntegrityException("The risk draft history differs from its hydrated current authority.");
        return result.AsReadOnly();
    }
}
