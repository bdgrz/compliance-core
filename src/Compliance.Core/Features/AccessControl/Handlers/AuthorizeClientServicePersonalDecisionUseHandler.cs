using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The client service history pair of the generic acquire receiver: it holds the exact complete service history and epoch a personal decision pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeClientServicePersonalDecisionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseAuthorizeHandler<ClientServiceIndependence, AuthorizeClientServicePersonalDecisionUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(AuthorizeClientServicePersonalDecisionUse);

    protected override ClientServiceIndependence Open(AuthorizeClientServicePersonalDecisionUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override ValueTask<Result> VerifyPinnedAsync(ClientServiceIndependence owner,
        SourceUseRequirement requirement, CancellationToken ct) =>
        ValueTask.FromResult(IsPinned(owner, requirement)
            ? Result.Success
            : Stale("The service epoch advanced after the decision was admitted."));

    protected override bool IsPinned(ClientServiceIndependence owner, SourceUseRequirement requirement) =>
        owner.Epoch == requirement.ResourceRevision &&
        (owner.LatestService?.Metadata.EventId ?? Uuid.Empty) == requirement.ContentEvent.EventId;

    protected override Func<ClientServiceIndependence, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (ClientServiceIndependence owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizePersonalDecisionUse(provenance, intent, generation);
}
