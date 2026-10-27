using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The draft rule catalog pair of the generic acquire receiver: it holds the exact draft rule version a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeIndependenceRuleCatalogEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier, IndependenceRuleCatalogReader catalogs)
    : EngagementUseAuthorizeHandler<IndependenceRuleCatalog, AuthorizeIndependenceRuleCatalogEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(AuthorizeIndependenceRuleCatalogEngagementUse);

    protected override IndependenceRuleCatalog Open(AuthorizeIndependenceRuleCatalogEngagementUse request, Uuid selectedCommandId) =>
        new(selectedCommandId);

    protected override async ValueTask<Result> VerifyPinnedAsync(IndependenceRuleCatalog owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (!IsPinned(owner, requirement))
            return Stale("The rule catalog changed after the decision was admitted.");
        IndependenceRuleVersionView rules;
        try
        {
            rules = await Content.ReadJsonAsync<IndependenceRuleVersionView>(EngagementPersonalDecisionEffects.PlatformRealm,
                requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Stale($"The pinned rule content cannot be verified: {exception.Message}");
        }

        try
        {
            var draft = await catalogs.FindVersionAsync(rules.Version, ct).ConfigureAwait(false);
            return draft is not null && IndependenceSourceDigest.RuleContent(draft.Content) ==
                IndependenceSourceDigest.RuleContent(rules.Content)
                ? Result.Success
                : Stale("The pinned rule version is not the retained draft.");
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(IndependenceCatalogContent.Failure(error));
        }
    }

    protected override bool IsPinned(IndependenceRuleCatalog owner, SourceUseRequirement requirement) =>
        owner.LatestRevision is { } latest && requirement.ContentEvent.Stream == owner.Stream &&
        latest.Metadata.EventId == requirement.ContentEvent.EventId && latest.Metadata.AggregateVersion == requirement.ContentEvent.AggregateVersion &&
        (ulong)owner.Sequence == requirement.ResourceRevision;

    protected override Func<IndependenceRuleCatalog, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (IndependenceRuleCatalog owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementUse(provenance, intent, generation);
}
