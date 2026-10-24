using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Settles the use of the duty designation after the irreversible personal decision (irreversible decision) or after
///     cancellation (canceled). <see cref="Source" /> is the accepted admission fact; the receiver resolves the
///     terminal fact from the engagement owner and never infers one from missing native state.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.settle-firm-professional-duty-designation-engagement-use", 1)]
public sealed record SettleFirmProfessionalDutyDesignationEngagementUse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid SourceOwnerId,
    Uuid EngagementId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IEngagementPersonalDecisionUse, IEngagementPersonalDecisionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
