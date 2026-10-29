using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Changes.Contracts;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Persistence;
using Bdgrz.Compliance.Features.Changes.Projections;
using Bdgrz.Compliance.Features.Changes.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Changes.Readers;

/// <summary>Reconstructs exact verified business content at a native owner cut or recorded-through receipt cut.</summary>
public sealed class SignificantChangeReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, INativeEventReceiptReader? receipts = null)
{
    public async ValueTask<SignificantChangeReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid changeId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SignificantChange(tenantId, programId, changeId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, null, ct).ConfigureAwait(false);
    }
    public async ValueTask<SignificantChangeRules> RulesAsync(Uuid tenantId, Uuid programId, Uuid changeId, CancellationToken ct = default) =>
        (await ReadAsync(tenantId, programId, changeId, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<SignificantChangeReadSnapshot> ReadThroughAsync(Uuid tenantId, Uuid programId, Uuid changeId,
        DateTimeOffset recordedThrough, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SignificantChange(tenantId, programId, changeId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, recordedThrough, ct).ConfigureAwait(false);
    }
    internal ValueTask<SignificantChangeReadSnapshot> ReadAsync(SignificantChange owner, Uuid programId, CancellationToken ct) =>
        ReadAsync(owner, programId, null, ct);
    async ValueTask<SignificantChangeReadSnapshot> ReadAsync(SignificantChange owner, Uuid programId,
        DateTimeOffset? through, CancellationToken ct)
    {
        var rules = new SignificantChangeRules(owner.TenantId, programId, owner.Id);
        if (owner.Header is null || owner.ProgramId != programId)
            return new(owner, rules);
        var sequence = 0L;
        ISignificantChangeFact? last = null;
        var observedSequence = 0L;
        var excluded = false;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(),
            SignificantChange.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not ISignificantChangeFact fact || fact.NativeSequence != observedSequence + 1 ||
                record.Stream != owner.Stream || fact.Header.ProgramId != programId)
                throw new ImmutableContentIntegrityException("The change history has another native sequence, scope or receipt order.");
            if (fact.NativeSequence > owner.NativeSequence)
                break;
            observedSequence = fact.NativeSequence;
            if (through is { } cutoff)
            {
                if (receipts is null)
                    throw new ImmutableContentUnavailableException("The exact broker receipt reader is not configured for this recorded-through query.");
                var recordedAt = await receipts.RecordedAtAsync(record, ct).ConfigureAwait(false);
                if (recordedAt > cutoff)
                { excluded = true; continue; }
                if (excluded)
                    throw new ImmutableContentIntegrityException("The broker receipt cutoff does not select a complete causal prefix of the owner history.");
            }
            var mutation = await content.ReadJsonAsync<SignificantChangeMutation>(owner.TenantId.ToString(), fact.Change, ct).ConfigureAwait(false);
            var body = await content.ReadJsonAsync<SignificantChangeView>(owner.TenantId.ToString(), fact.CurrentContent, ct).ConfigureAwait(false);
            if (fact.Change.Schema != SignificantChangeContentStore.ChangeSchema || fact.CurrentContent.Schema != SignificantChangeContentStore.BodySchema ||
                mutation.Kind != fact.Operation.CommandKind || fact.ChangeCount != (mutation.Changed ? 1 : 0) ||
                !SignificantChangeContentStore.ValidMutation(rules.Current, body, mutation) ||
                SignificantChangeContentStore.Header(body) != fact.Header)
                throw new ImmutableContentIntegrityException("The change immutable body differs from its native transition or authority.");
            SignificantChangeContentStore.Apply(rules, mutation);
            sequence = fact.NativeSequence;
            last = fact;
        }
        if (observedSequence != owner.NativeSequence)
            throw new ImmutableContentUnavailableException("The complete captured native source cut is not readable yet.");
        if (through is null && (sequence != owner.NativeSequence || last is null || last.Header != owner.Header || last.CurrentContent != owner.CurrentContent))
            throw new ImmutableContentUnavailableException("The complete exact native change cut is not readable yet.");
        return new(owner, rules);
    }
}
