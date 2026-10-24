using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The client service history pair of the generic acquire receiver: it holds the exact complete service history and epoch a personal decision pinned at admission. A stale
///     or ineligible source rejects the personal decision and fences the key through the abort path.
/// </summary>
sealed class AuthorizeClientServicePersonalDecisionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementPersonalDecisionVerifier verifier)
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
