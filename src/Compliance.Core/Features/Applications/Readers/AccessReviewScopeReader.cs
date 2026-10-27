using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Verified source history and effective-date composition outside compact scope authority.</summary>
public static class AccessReviewScopeReader
{
    public const string DecisionSchema = "system-instance-access-review-scope-decision.v2";

    public static async Task<IReadOnlyList<AccessReviewScopeDecisionView>> ReadAsync(IAggregateReader reader,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenant, Uuid application, Uuid instance,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SystemInstanceAccessReviewScope(tenant, instance), ct).ConfigureAwait(false);
        var facts = await NativeOwnerHistory.ReadAsync<AccessReviewScopeDecided>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The scope history differs from its hydrated exact source cut.");
        var decisions = new List<AccessReviewScopeDecisionView>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.TenantId != tenant || fact.ApplicationId != application || fact.SystemInstanceId != instance ||
                fact.Sequence != decisions.Count + 1 || decisions.Count > 0 && fact.EffectiveFrom < decisions[^1].EffectiveFrom)
                throw new ImmutableContentIntegrityException("The scope source has another parent, instance or effective-date sequence.");
            decisions.Add(await ReadDecisionAsync(content, fact, ct).ConfigureAwait(false));
        }
        return decisions;
    }

    public static async Task<AccessReviewScopeDecisionView> ReadDecisionAsync(IImmutableContentStore content,
        AccessReviewScopeDecided fact, CancellationToken ct = default)
    {
        var owner = new SystemInstanceAccessReviewScope(fact.TenantId, fact.SystemInstanceId);
        if (fact.Content is null || fact.Operation is null || fact.Operation.Actor is null || fact.Content.Schema != DecisionSchema || fact.Sequence < 1 || fact.SystemInstanceRevision < 1 ||
            fact.DecisionId != fact.Operation.LogicalId || fact.FirstResponse is null || fact.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(DecideAccessReviewScope), (ulong)(fact.Sequence - 1)))
            throw new ImmutableContentIntegrityException("The scope decision has another exact native root.");
        var decision = await content.ReadJsonAsync<AccessReviewScopeDecisionView>(fact.TenantId.ToString(), fact.Content, ct)
            .ConfigureAwait(false);
        if (decision.ApprovedBy is null || decision.ApprovedBy.Kind != "member" ||
            !Uuid.TryParse(decision.ApprovedBy.Id, null, out var memberId) || memberId == Uuid.Empty ||
            decision.TenantId != fact.TenantId || decision.ApplicationId != fact.ApplicationId ||
            decision.SystemInstanceId != fact.SystemInstanceId || decision.SystemInstanceRevision != fact.SystemInstanceRevision ||
            decision.DecisionId != fact.DecisionId || decision.Sequence != fact.Sequence ||
            decision.EffectiveFrom != fact.EffectiveFrom || decision.ApprovedBy != fact.Operation.Actor ||
            decision.DecidedAt != fact.Operation.AcceptedAt || decision.Decision is not ("included" or "excluded") ||
            string.IsNullOrWhiteSpace(decision.Reason) || decision.Reason != decision.Reason.Trim() || decision.Reason.Length > 2000 ||
            decision.ReviewBy is { } review && review <= decision.EffectiveFrom)
            throw new ImmutableContentIntegrityException("The scope body differs from its exact source decision and content rules.");
        return decision;
    }

    public static AccessReviewScopeView View(Uuid tenant, Uuid application, Uuid instance,
        IReadOnlyList<AccessReviewScopeDecisionView> decisions, DateTimeOffset asOf)
    {
        var effective = AccessReviewScopeStatus.EffectiveAt(decisions, asOf);
        return new AccessReviewScopeView(tenant, application, instance, asOf,
            effective?.Decision ?? AccessReviewScopeStatus.Unresolved, effective, decisions);
    }
}
