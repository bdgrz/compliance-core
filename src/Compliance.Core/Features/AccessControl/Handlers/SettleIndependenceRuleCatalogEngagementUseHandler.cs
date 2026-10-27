using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The draft rule catalog pair of the generic settle receiver. The terminal fact is resolved from the
///     engagement owner; a settlement before any grant fences the key so a delayed acquisition is refused.
/// </summary>
sealed class SettleIndependenceRuleCatalogEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseSettleHandler<IndependenceRuleCatalog, SettleIndependenceRuleCatalogEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(SettleIndependenceRuleCatalogEngagementUse);

    protected override IndependenceRuleCatalog Open(SettleIndependenceRuleCatalogEngagementUse request, Uuid selectedCommandId) =>
        new(selectedCommandId);

    protected override Func<IndependenceRuleCatalog, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (IndependenceRuleCatalog owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettleEngagementUse(provenance, intent, generation);
}
