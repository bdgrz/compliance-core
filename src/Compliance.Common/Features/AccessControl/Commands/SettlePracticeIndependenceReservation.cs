using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

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
