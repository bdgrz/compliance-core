using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The ratification catalog pair of the generic acquire receiver: it holds the exact ratification of the rule version a partner evaluation pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeIndependenceRuleRatificationCatalogEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier, IndependenceRuleRatificationReader catalogs)
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

        try
        {
            var retained = (await catalogs.ReadAsync(owner, ct).ConfigureAwait(false)).Active;
            return retained == ratification
                ? Result.Success
                : Stale("The pinned ratification is not the retained one.");
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(IndependenceCatalogContent.Failure(error));
        }
    }

    protected override bool IsPinned(IndependenceRuleRatificationCatalog owner, SourceUseRequirement requirement) =>
        owner.LatestRatification is { } latest && requirement.ContentEvent.Stream == owner.Stream &&
        latest.Metadata.EventId == requirement.ContentEvent.EventId && latest.Metadata.AggregateVersion == requirement.ContentEvent.AggregateVersion &&
        (ulong)owner.Sequence == requirement.ResourceRevision;

    protected override Func<IndependenceRuleRatificationCatalog, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (IndependenceRuleRatificationCatalog owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementUse(provenance, intent, generation);
}
