using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Launches completion tracking for the exact latest training requirement version.
///     <paramref name="CampaignId" /> is the logical operation identity.
/// </summary>
[Discriminator("bdgrz.training_campaign.launch", 2)]
public sealed record LaunchTrainingCampaign(Uuid CampaignId, Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long RequirementVersion, Uuid RosterSnapshotId, DateOnly DueOn,
    string? Instructions = null)
    : IRequest<CampaignRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CampaignId;
}
