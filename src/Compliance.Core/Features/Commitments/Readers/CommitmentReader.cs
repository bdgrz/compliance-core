using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Projections;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
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
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Commitments.Readers;

/// <summary>Verified current and historical read composition at the hydrated native owner cut.</summary>
public sealed class CommitmentReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<CommitmentReadSnapshot> ReadAsync(Uuid tenant, Uuid draft, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new CommitmentDraft(tenant, draft), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<CommitmentReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid draft, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new CommitmentDraft(tenant, draft), ct).ConfigureAwait(false);
        return await ReadAsync(owner, program, ct).ConfigureAwait(false);
    }
    public async ValueTask<CommitmentRules> RulesAsync(Uuid tenant, Uuid draft, CancellationToken ct = default) =>
        (await ReadAsync(tenant, draft, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<CommitmentRules> RulesAsync(Uuid tenant, Uuid program, Uuid draft, CancellationToken ct = default) =>
        (await ReadAsync(tenant, program, draft, ct).ConfigureAwait(false)).Rules;
    internal async ValueTask<CommitmentReadSnapshot> ReadAsync(CommitmentDraft owner, Uuid program, CancellationToken ct)
    {
        var rules = new CommitmentRules(owner.TenantId, owner.Id);
        if (!owner.IsCreated || owner.ProgramId != program)
            return new CommitmentReadSnapshot(owner, rules, false);
        var sequence = 0L;
        ICommitmentFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), CommitmentDraft.Area,
            owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not ICommitmentFact fact || fact.NativeSequence != sequence + 1 || fact.NativeSequence > owner.NativeSequence)
                throw new ImmutableContentIntegrityException("The commitment source has another exact native acceptance sequence.");
            var mutation = await CommitmentFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
            foreach (var change in mutation.Changes)
                rules.Apply(change);
            var current = await CommitmentFactContent.ReadContentAsync(content, fact, ct).ConfigureAwait(false);
            if (rules.CurrentContent is null || ContentDigest.OfJson(current) != ContentDigest.OfJson(rules.CurrentContent) ||
                rules.Header(fact.Header.Content) != fact.Header || fact.Authority.Schema != CommitmentFactContent.AuthoritySchema)
                throw new ImmutableContentIntegrityException("The commitment content and compact current authority differ.");
            var authority = await content.ReadJsonAsync<CommitmentAuthorityContent>(owner.TenantId.ToString(), fact.Authority, ct).ConfigureAwait(false);
            if (ContentDigest.OfJson(authority) != ContentDigest.OfJson(rules.Authority(fact.Header)))
                throw new ImmutableContentIntegrityException("The commitment author, responsibility or decision authority differs from its source.");
            last = fact;
            sequence = fact.NativeSequence;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact commitment native source cut is not visible yet.");
        if (last.Header != owner.Header || last.Authority != owner.CurrentAuthority)
            throw new ImmutableContentIntegrityException("The commitment read differs from its hydrated exact owner cut.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new CommitmentReadSnapshot(owner, rules);
    }
}
