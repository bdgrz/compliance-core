using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Settles the temporary reservation of one practice subject after the operation recorded its terminal fact or was
///     rejected. A settlement before any reservation fences the key against late acquisition. It never removes a
///     permanent commitment.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.settle-practice-independence-reservation", 1)]
public sealed record SettlePracticeIndependenceReservation(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    PracticeSubjectKind SubjectKind, Uuid SubjectId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IEngagementAcceptanceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    Uuid ISourceUseSettlement.SourceOwnerId => SubjectId;
}
