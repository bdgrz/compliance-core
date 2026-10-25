using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Authoritative current and immutable history reads at the owner's exact physical cut.</summary>
public sealed class RiskMethodReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async Task<RiskMethodVersionView?> CurrentAsync(Uuid tenant, Uuid program, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskMethod(tenant, program), ct).ConfigureAwait(false);
        return owner.Latest is { } fact ? await RiskMethodContent.ReadAsync(content, fact, ct).ConfigureAwait(false) : null;
    }

    public async Task<RiskMethodReadSnapshot> ReadAsync(Uuid tenant, Uuid program, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new RiskMethod(tenant, program), ct).ConfigureAwait(false);
        var facts = await NativeOwnerHistory.ReadAsync<RiskMethodVersionPublished>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.PublishedVersion || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The method history differs from its exact hydrated current cut.");
        var versions = new List<RiskMethodVersionView>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.TenantId != tenant || fact.ProgramId != program || fact.PublishedVersion != versions.Count + 1)
                throw new ImmutableContentIntegrityException("The method history has another scope or sequential version.");
            versions.Add(await RiskMethodContent.ReadAsync(content, fact, ct).ConfigureAwait(false));
        }
        return new RiskMethodReadSnapshot(versions.AsReadOnly(), facts);
    }
}
