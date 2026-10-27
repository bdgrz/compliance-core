using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

public sealed class IndependenceRuleCatalogReader(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<IndependenceRuleCatalogReadSnapshot> ReadAsync(CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    public async ValueTask<IndependenceRuleCatalogReadSnapshot> ReadAsync(IndependenceRuleCatalog owner, CancellationToken ct = default)
    {
        if (owner.Header is null)
            return new(owner, null);
        var value = await ReadNodeAsync(owner.CurrentContent!, ct).ConfigureAwait(false);
        if (value.Sequence != owner.Sequence || IndependenceCatalogContent.Header(value) != owner.Header || owner.LatestRevision is not { } latest || latest.RuleContent != owner.CurrentContent ||
            latest.RequestId != value.RequestId || latest.Operation.AuthoredIntentSha256 != IndependenceCatalogContent.Intent(value))
            throw new ImmutableContentIntegrityException("The current authored rules differ from their exact native coordinator cut.");
        return new(owner, value);
    }
    public async ValueTask<IndependenceRuleVersionView?> FindVersionAsync(long version, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        if (version < 1 || version > owner.Sequence)
            return null;
        var current = await ReadAsync(owner, ct).ConfigureAwait(false);
        var node = current.Content!;
        while (node.Sequence > version)
            node = await PreviousAsync(node, ct).ConfigureAwait(false);
        return node.Version;
    }
    public async ValueTask<IReadOnlyList<IndependenceRuleVersionView>> ReadVersionsAsync(CancellationToken ct = default)
    {
        var current = await ReadAsync(ct).ConfigureAwait(false);
        if (current.Content is null)
            return [];
        var result = new List<IndependenceRuleVersionView>();
        var node = current.Content;
        while (true)
        {
            result.Add(node.Version);
            if (node.Sequence == 1)
                break;
            node = await PreviousAsync(node, ct).ConfigureAwait(false);
        }
        result.Reverse();
        return result.AsReadOnly();
    }
    async ValueTask<IndependenceRuleVersionContent> PreviousAsync(IndependenceRuleVersionContent current, CancellationToken ct)
    {
        if (current.Previous is null)
            throw new ImmutableContentIntegrityException("The authored rules history has no exact predecessor.");
        var previous = await ReadNodeAsync(current.Previous, ct).ConfigureAwait(false);
        if (previous.Sequence != current.Sequence - 1)
            throw new ImmutableContentIntegrityException("The authored rules history has another consecutive version cut.");
        return previous;
    }
    async ValueTask<IndependenceRuleVersionContent> ReadNodeAsync(ImmutableContentRef reference, CancellationToken ct)
    {
        if (!IndependenceCatalogContent.Reference(reference, IndependenceCatalogContent.RuleSchema))
            throw new ImmutableContentIntegrityException("The authored rules source has another exact immutable schema.");
        var value = await content.ReadJsonAsync<IndependenceRuleVersionContent>(IndependenceCatalogContent.Realm, reference, ct).ConfigureAwait(false);
        if (!IndependenceCatalogContent.ValidRule(value) || !IndependenceCatalogContent.Matches(reference, value, IndependenceCatalogContent.RuleSchema))
            throw new ImmutableContentIntegrityException("The authored rules immutable source has another owner, attribution or version.");
        return value with { Version = IndependenceRecordValidation.Freeze(value.Version) };
    }
}
