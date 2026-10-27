using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Temporarily and exclusively reserves one practice subject (a staff member or a canonical user) for the
///     frozen practice. <see cref="Source" /> is the accepted root admission. The reservation does not wait for the
///     service epoch grant and grants no permanent authority; an opposite permanent practice rejects the operation.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.reserve-practice-independence", 1)]
public sealed record ReservePracticeIndependence(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, PracticeSubjectKind SubjectKind,
    Uuid SubjectId, string Practice)
    : ISourceUseAuthorization, IEngagementAcceptanceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    Uuid ISourceUseAuthorization.SourceOwnerId => SubjectId;

    Uuid ISourceUseAuthorization.SourceContentEventId => Uuid.Empty;

    ulong ISourceUseAuthorization.ExpectedContentRevision => 0;
}
