using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The ratification catalog pair of the generic acquire receiver: it holds the exact ratification of the rule version a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeIndependenceRuleRatificationCatalogEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseAuthorizeHandler<IndependenceRuleRatificationCatalog, AuthorizeIndependenceRuleRatificationCatalogEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse);

    protected override IndependenceRuleRatificationCatalog Open(AuthorizeIndependenceRuleRatificationCatalogEngagementUse request, Uuid selectedCommandId) =>
        new(selectedCommandId);

    protected override async ValueTask<Result> VerifyPinnedAsync(IndependenceRuleRatificationCatalog owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (!IsPinned(owner, requirement))
            return Stale("The ratification catalog changed after the decision was admitted.");
        IndependenceRuleRatificationView ratification;
        try
        {
            ratification = await Content.ReadJsonAsync<IndependenceRuleRatificationView>(
                EngagementPersonalDecisionEffects.PlatformRealm, requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Stale($"The pinned ratification content cannot be verified: {exception.Message}");
        }

        return owner.ForVersion(ratification.RuleVersion) == ratification
            ? Result.Success
            : Stale("The pinned ratification is not the retained one.");
    }

    protected override bool IsPinned(IndependenceRuleRatificationCatalog owner, SourceUseRequirement requirement) =>
        owner.LatestRatification is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Sequence == requirement.ResourceRevision;

    protected override Func<IndependenceRuleRatificationCatalog, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (IndependenceRuleRatificationCatalog owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementUse(provenance, intent, generation);
}
