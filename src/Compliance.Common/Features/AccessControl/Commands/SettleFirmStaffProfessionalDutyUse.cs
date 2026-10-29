using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
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
