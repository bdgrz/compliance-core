using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Applies one admitted participant result to the participant owner of <c>(CampaignId, PersonId)</c>.
///     <paramref name="Source" /> is the <see cref="PolicyParticipantResultAdmitted" /> fact; the receiver verifies it
///     by hydrating the campaign and applies exactly the frozen <c>ChildAdmissionIntent</c>. Exactly one native
///     acknowledgement, completion or waiver is raised, selected by <paramref name="ResultKind" />.
/// </summary>
[Discriminator("bdgrz.policy-campaign-participant.apply-policy-participant-result", 1)]
public sealed record ApplyPolicyParticipantResult(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid CampaignId, Uuid PersonId, CampaignParticipantResultKind ResultKind)
    : IRequest<ReceiverOutcome>, IPolicyCampaignReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
