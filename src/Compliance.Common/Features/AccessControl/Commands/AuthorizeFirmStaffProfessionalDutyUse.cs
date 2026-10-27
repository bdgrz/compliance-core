using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Acquires the exact currently active canonical staff record for recording a designation. The staff record
///     refuses status changes while the use is held. <see cref="Source" /> is the accepted change fact; the receiver
///     verifies it by hydrating the designation owner.
/// </summary>
[Discriminator("bdgrz.firm-staff-record.authorize-professional-duty-use", 1)]
public sealed record AuthorizeFirmStaffProfessionalDutyUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, ulong ExpectedContentRevision, Uuid DesignationId)
    : ISourceUseAuthorization, IProfessionalDutyChangeReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
