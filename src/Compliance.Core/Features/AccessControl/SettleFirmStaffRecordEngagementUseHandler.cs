using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The staff-record pair of the generic settle receiver. The terminal fact is resolved from the
///     engagement owner; a settlement before any grant fences the key so a delayed acquisition is refused.
/// </summary>
sealed class SettleFirmStaffRecordEngagementUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : EngagementUseSettleHandler<FirmStaffRecord, SettleFirmStaffRecordEngagementUse>(reader, executor, content, clock, verifier)
{
    protected override string CommandKind => nameof(SettleFirmStaffRecordEngagementUse);

    protected override FirmStaffRecord Open(SettleFirmStaffRecordEngagementUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override Func<FirmStaffRecord, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (FirmStaffRecord owner, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            owner.SettleEngagementUse(provenance, intent, generation);
}
