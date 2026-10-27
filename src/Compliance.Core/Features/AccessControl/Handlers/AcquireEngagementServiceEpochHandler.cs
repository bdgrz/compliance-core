using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
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
///     The service epoch pair of the generic acquire receiver: it holds the exact complete service history and epoch
///     an acceptance or staff assignment pinned at admission until the operation reaches a terminal fact. A service
///     recorded after admission rejects the operation and fences the key through the abort path.
/// </summary>
sealed class AcquireEngagementServiceEpochHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseAuthorizeHandler<ClientServiceIndependence, AcquireEngagementServiceEpoch>(reader, executor, content,
        clock, verifier)
{
    protected override string CommandKind => nameof(AcquireEngagementServiceEpoch);

    protected override ClientServiceIndependence Open(AcquireEngagementServiceEpoch request, Uuid selectedCommandId) =>
        new(EngagementPersonalDecisionVerifier.TenantOf(request.Target), selectedCommandId);

    protected override ValueTask<Result> VerifyPinnedAsync(ClientServiceIndependence owner,
        SourceUseRequirement requirement, CancellationToken ct) =>
        ValueTask.FromResult(IsPinned(owner, requirement)
            ? Result.Success
            : Stale("The service epoch advanced after the operation was admitted."));

    protected override bool IsPinned(ClientServiceIndependence owner, SourceUseRequirement requirement) =>
        owner.Epoch == requirement.ResourceRevision &&
        (owner.LatestService?.Metadata.EventId ?? Uuid.Empty) == requirement.ContentEvent.EventId;

    protected override Func<ClientServiceIndependence, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (ClientServiceIndependence owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.AuthorizeEngagementEpoch(provenance, intent, generation);
}
