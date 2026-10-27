using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The service epoch pair of the generic settle receiver. The terminal fact is resolved from the engagement owner;
///     a settlement before any grant fences the key so a delayed acquisition is refused. Permanent practice
///     commitments are never touched.
/// </summary>
sealed class ReleaseEngagementServiceEpochHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseSettleHandler<ClientServiceIndependence, ReleaseEngagementServiceEpoch>(reader, executor, content,
        clock, verifier)
{
    protected override string CommandKind => nameof(ReleaseEngagementServiceEpoch);

    protected override ClientServiceIndependence Open(ReleaseEngagementServiceEpoch request, Uuid selectedCommandId) =>
        new(EngagementPersonalDecisionVerifier.TenantOf(request.Target), selectedCommandId);

    protected override Func<ClientServiceIndependence, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (ClientServiceIndependence owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettleEngagementEpoch(provenance, intent, generation);
}
