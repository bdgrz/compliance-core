using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Readers;

/// <summary>Verified full read composition over native source facts and their immutable bodies, outside command state.</summary>
public sealed class PolicyReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<PolicyReadSnapshot> ReadAsync(Uuid tenant, Uuid policy, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new Policy(tenant, policy), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<PolicyReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid policy, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new Policy(tenant, policy), ct).ConfigureAwait(false);
        return await ReadAsync(owner, program, ct).ConfigureAwait(false);
    }
    public async ValueTask<PolicyRules> RulesAsync(Uuid tenant, Uuid policy, CancellationToken ct = default) =>
        (await ReadAsync(tenant, policy, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<PolicyRules> RulesAsync(Uuid tenant, Uuid program, Uuid policy, CancellationToken ct = default) =>
        (await ReadAsync(tenant, program, policy, ct).ConfigureAwait(false)).Rules;

    internal async ValueTask<PolicyReadSnapshot> ReadAsync(Policy owner, Uuid program, CancellationToken ct)
    {
        var rules = new PolicyRules(owner.TenantId, owner.Id);
        if (!owner.IsCreated || owner.ProgramId != program)
            return new PolicyReadSnapshot(owner, rules, false);
        var sequence = 0L;
        IPolicyFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(), Policy.Area,
            owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IPolicyFact fact || fact.NativeSequence != sequence + 1 || fact.NativeSequence > owner.NativeSequence)
                throw new ImmutableContentIntegrityException("The Policy source has another exact native acceptance sequence.");
            var mutation = await PolicyFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
            rules.Apply(mutation.Change);
            var current = await PolicyFactContent.ReadContentAsync(content, fact, ct).ConfigureAwait(false);
            if (rules.CurrentContent is null || PolicyRules.Hash(current) != PolicyRules.Hash(rules.CurrentContent) ||
                rules.Header(fact.Header.Content) != fact.Header || fact.Authority.Schema != PolicyFactContent.AuthoritySchema)
                throw new ImmutableContentIntegrityException("The Policy body and compact source authority differ.");
            var authority = await content.ReadJsonAsync<PolicyAuthorityContent>(owner.TenantId.ToString(), fact.Authority, ct).ConfigureAwait(false);
            if (ContentDigest.OfJson(authority) != ContentDigest.OfJson(rules.Authority(fact.Header)))
                throw new ImmutableContentIntegrityException("The Policy current author and content authority differs from its source.");
            last = fact;
            sequence = fact.NativeSequence;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact Policy native source cut is not visible yet.");
        if (last.Header != owner.Header || last.Authority != owner.CurrentAuthority)
            throw new ImmutableContentIntegrityException("The reconstructed Policy differs from its hydrated native owner cut.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new PolicyReadSnapshot(owner, rules);
    }
}
