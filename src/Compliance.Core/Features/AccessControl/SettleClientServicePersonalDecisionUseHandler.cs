using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The client service history pair of the generic settle receiver. The terminal fact is resolved from the
///     engagement owner; a settlement before any grant fences the key so a delayed acquisition is refused.
/// </summary>
sealed class SettleClientServicePersonalDecisionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementPersonalDecisionVerifier verifier)
    : EngagementUseSettleHandler<ClientServiceIndependence, SettleClientServicePersonalDecisionUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(SettleClientServicePersonalDecisionUse);

    protected override ClientServiceIndependence Open(SettleClientServicePersonalDecisionUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override Func<ClientServiceIndependence, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (ClientServiceIndependence owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettlePersonalDecisionUse(provenance, intent, generation);
}
