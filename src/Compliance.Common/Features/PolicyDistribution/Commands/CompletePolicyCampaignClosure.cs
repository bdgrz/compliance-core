using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Completes the pending campaign closure once every admitted participant result is applied and every
///     participant use is held. <paramref name="Source" /> is the accepted <see cref="PolicyCampaignClosureAccepted" />
///     fact; <paramref name="ParticipantProofManifest" /> is the frozen manifest of the exact uses the receiver finds
///     granted on the participant owners.
/// </summary>
[Discriminator("bdgrz.policy-distribution-campaign.complete-policy-campaign-closure", 1)]
public sealed record CompletePolicyCampaignClosure(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid CampaignId, ImmutableContentRef ParticipantProofManifest)
    : IRequest<ReceiverOutcome>, IPolicyCampaignReceiver, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
