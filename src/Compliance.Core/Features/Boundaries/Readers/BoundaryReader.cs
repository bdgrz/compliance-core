using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Readers;

/// <summary>Verified current and historical read composition at the hydrated native owner cut.</summary>
public sealed class BoundaryReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<BoundaryReadSnapshot> ReadAsync(Uuid tenant, Uuid draft, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SystemBoundary(tenant, draft), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<BoundaryReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid draft, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SystemBoundary(tenant, draft), ct).ConfigureAwait(false);
        return await ReadAsync(owner, program, ct).ConfigureAwait(false);
    }
    public async ValueTask<BoundaryRules> RulesAsync(Uuid tenant, Uuid draft, CancellationToken ct = default) =>
        (await ReadAsync(tenant, draft, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<BoundaryRules> RulesAsync(Uuid tenant, Uuid program, Uuid draft, CancellationToken ct = default) =>
        (await ReadAsync(tenant, program, draft, ct).ConfigureAwait(false)).Rules;
    internal async ValueTask<BoundaryReadSnapshot> ReadAsync(SystemBoundary owner, Uuid program, CancellationToken ct)
    {
        var rules = new BoundaryRules(owner.TenantId, owner.Id);
        if (!owner.IsCreated || owner.ProgramId != program)
            return new BoundaryReadSnapshot(owner, rules, false);
        var sequence = 0L;
        IBoundaryFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), SystemBoundary.Area,
            owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IBoundaryFact fact || fact.NativeSequence != sequence + 1 || fact.NativeSequence > owner.NativeSequence)
                throw new ImmutableContentIntegrityException("The boundary source has another exact native acceptance sequence.");
            var mutation = await BoundaryFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
            foreach (var change in mutation.Changes)
                rules.Apply(change);
            var current = await BoundaryFactContent.ReadContentAsync(content, fact, ct).ConfigureAwait(false);
            if (rules.CurrentContent is null || ContentDigest.OfJson(current) != ContentDigest.OfJson(rules.CurrentContent) ||
                rules.Header(fact.Header.Content) != fact.Header || fact.Authority.Schema != BoundaryFactContent.AuthoritySchema)
                throw new ImmutableContentIntegrityException("The boundary content and compact current authority differ.");
            var authority = await content.ReadJsonAsync<BoundaryAuthorityContent>(owner.TenantId.ToString(), fact.Authority, ct).ConfigureAwait(false);
            if (ContentDigest.OfJson(authority) != ContentDigest.OfJson(rules.Authority(fact.Header)))
                throw new ImmutableContentIntegrityException("The boundary author, responsibility or decision authority differs from its source.");
            last = fact;
            sequence = fact.NativeSequence;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact boundary native source cut is not visible yet.");
        if (last.Header != owner.Header || last.Authority != owner.CurrentAuthority)
            throw new ImmutableContentIntegrityException("The boundary read differs from its hydrated exact owner cut.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new BoundaryReadSnapshot(owner, rules);
    }
}
