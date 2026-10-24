using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The duty-designation pair of the generic settle receiver. The terminal fact is resolved from the
///     engagement owner; a settlement before any grant fences the key so a delayed acquisition is refused.
/// </summary>
sealed class SettleFirmProfessionalDutyDesignationEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseSettleHandler<FirmProfessionalDutyDesignation, SettleFirmProfessionalDutyDesignationEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(SettleFirmProfessionalDutyDesignationEngagementUse);

    protected override FirmProfessionalDutyDesignation Open(SettleFirmProfessionalDutyDesignationEngagementUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, FirmProfessionalDuty.EngagementPartner,
            EngagementPersonalDecisionVerifier.TenantOf(request.Source.Stream), selectedCommandId);

    protected override Func<FirmProfessionalDutyDesignation, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (FirmProfessionalDutyDesignation owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettleEngagementUse(provenance, intent, generation);
}
