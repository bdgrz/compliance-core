using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

public sealed class IndependenceRuleRatificationReader(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<IndependenceRuleRatificationReadSnapshot> ReadAsync(CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    public async ValueTask<IndependenceRuleRatificationReadSnapshot> ReadAsync(IndependenceRuleRatificationCatalog owner, CancellationToken ct = default)
    {
        if (owner.Header is null)
            return new(owner, null);
        var value = await ReadNodeAsync(owner.CurrentContent!, ct).ConfigureAwait(false);
        if (value.Sequence != owner.Sequence || IndependenceCatalogContent.Header(value) != owner.Header ||
            owner.LatestRatification is not { } latest || latest.RatificationContent != owner.CurrentContent || latest.RequestId != value.RequestId || latest.Operation.AuthoredIntentSha256 != IndependenceCatalogContent.Intent(value))
            throw new ImmutableContentIntegrityException("The active personal ratification differs from its exact native coordinator cut.");
        return new(owner, value);
    }
    public async ValueTask<IndependenceRuleRatificationView?> ForVersionAsync(long ruleVersion, CancellationToken ct = default)
    {
        var current = await ReadAsync(ct).ConfigureAwait(false);
        var node = current.Content;
        while (node is not null && node.Ratification.RuleVersion >= ruleVersion)
        {
            if (node.Ratification.RuleVersion == ruleVersion)
                return node.Ratification;
            node = node.Sequence > 1 ? await PreviousAsync(node, ct).ConfigureAwait(false) : null;
        }
        return null;
    }
    public async ValueTask<IReadOnlyList<IndependenceRuleRatificationView>> ReadHistoryAsync(CancellationToken ct = default)
    {
        var current = await ReadAsync(ct).ConfigureAwait(false);
        if (current.Content is null)
            return [];
        var result = new List<IndependenceRuleRatificationView>();
        var node = current.Content;
        while (true)
        {
            result.Add(node.Ratification);
            if (node.Sequence == 1)
                break;
            node = await PreviousAsync(node, ct).ConfigureAwait(false);
        }
        result.Reverse();
        return result.AsReadOnly();
    }
    public async ValueTask<IReadOnlyList<IndependenceRatificationHistoryRead>> IdentityEvidenceAsync(IndependenceRuleRatificationReadSnapshot current,
        Uuid ratificationId, CancellationToken ct = default)
    {
        var result = new List<IndependenceRatificationHistoryRead>();
        var reference = current.Owner.CurrentContent;
        var node = current.Content;
        while (node is not null && reference is not null)
        {
            result.Add(new(reference, node));
            if (node.Ratification.RatificationId == ratificationId || node.Sequence == 1)
                break;
            reference = node.Previous;
            node = await PreviousAsync(node, ct).ConfigureAwait(false);
        }
        IndependenceCatalogHistory.ProvesAbsent(current.Owner.CurrentContent, current.Sequence, result, ratificationId);
        return result.AsReadOnly();
    }
    async ValueTask<IndependenceRatificationContent> PreviousAsync(IndependenceRatificationContent current, CancellationToken ct)
    {
        if (current.Previous is null)
            throw new ImmutableContentIntegrityException("The personal ratification history has no exact predecessor.");
        var previous = await ReadNodeAsync(current.Previous, ct).ConfigureAwait(false);
        if (previous.Sequence != current.Sequence - 1 || previous.Ratification.RuleVersion >= current.Ratification.RuleVersion)
            throw new ImmutableContentIntegrityException("The personal ratification history has another monotonic activation cut.");
        return previous;
    }
    async ValueTask<IndependenceRatificationContent> ReadNodeAsync(ImmutableContentRef reference, CancellationToken ct)
    {
        if (!IndependenceCatalogContent.Reference(reference, IndependenceCatalogContent.RatificationSchema))
            throw new ImmutableContentIntegrityException("The personal ratification source has another exact immutable schema.");
        var value = await content.ReadJsonAsync<IndependenceRatificationContent>(IndependenceCatalogContent.Realm, reference, ct).ConfigureAwait(false);
        if (!IndependenceCatalogContent.ValidRatification(value) || !IndependenceCatalogContent.Matches(reference, value, IndependenceCatalogContent.RatificationSchema))
            throw new ImmutableContentIntegrityException("The personal ratification immutable source has another owner, identity or attribution.");
        return value;
    }
}
