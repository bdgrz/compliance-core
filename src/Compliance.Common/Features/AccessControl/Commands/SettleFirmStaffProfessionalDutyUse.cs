using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Settles the staff use after the recorded designation (irreversible decision) or after the rejection fence
///     (canceled). <see cref="Source" /> is the accepted change fact; the receiver resolves the terminal fact from the
///     designation owner and never infers a rejection from missing native state.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.settle-professional-duty-use", 1)]
public sealed record SettleFirmStaffProfessionalDutyUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid DesignationId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IProfessionalDutyChangeReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
