using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Rejects a pending acceptance before the irreversible authorization, fencing it against late delivery. An
///     authorized acceptance recovers forward and is never rejected, and no permanent practice commitment exists
///     before the authorization.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.reject-service-engagement-acceptance", 1)]
public sealed record RejectServiceEngagementAcceptance(Uuid CommandId, Uuid OperationId, EventStreamAddress Target,
    long Generation, long ExpectedRevision, ImmutableContentRef FailureProof)
    : IRequest<ReceiverOutcome>, IEngagementAcceptanceReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
