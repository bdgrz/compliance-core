using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

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
