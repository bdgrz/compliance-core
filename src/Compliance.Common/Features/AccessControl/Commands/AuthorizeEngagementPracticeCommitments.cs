using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     The irreversible commit point of an acceptance or staff assignment. <see cref="Source" /> is the accepted root
///     admission. The receiver re-proves every reservation and source use from its owner, judges every temporal
///     validity interval at the server-frozen decision instant and only then appends the authorization.
///     <paramref name="ReservationProofManifest" /> lists the practice reservation grants and
///     <paramref name="SourceUseProofManifest" /> the service epoch and source use grants, each in canonical key order.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.authorize-engagement-practice-commitments", 1)]
public sealed record AuthorizeEngagementPracticeCommitments(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    ImmutableContentRef ReservationProofManifest, ImmutableContentRef SourceUseProofManifest)
    : IRequest<ReceiverOutcome>, IEngagementAcceptanceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
