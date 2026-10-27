using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Permanently claims the complete verified assertion tuple. A claim bound to a different change rejects the
///     pending change. For recording, the receiver requires the exact granted staff use, resolved from the staff
///     record; <see cref="StaffEligibilityUse" /> names it when the caller already holds it.
/// </summary>
[Discriminator("bdgrz.professional-assertion-identity-claim.claim", 1)]
public sealed record ClaimProfessionalAssertionIdentity(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid AssertionId, Uuid DesignationId, string AssertionSha256, NativeFactReference? StaffEligibilityUse)
    : IRequest<ReceiverOutcome>, IProfessionalDutyChangeReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
