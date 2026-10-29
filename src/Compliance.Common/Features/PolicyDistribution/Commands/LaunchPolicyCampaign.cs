using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Launches acknowledgement of the exact current approved policy version, freezing the
///     audience from the given roster snapshot. <paramref name="CampaignId" /> is the logical operation identity.
/// </summary>
[Discriminator("bdgrz.policy_campaign.launch", 2)]
public sealed record LaunchPolicyCampaign(Uuid CampaignId, Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long PolicyVersion, Uuid RosterSnapshotId, DateOnly DueOn, string? Instructions = null)
    : IRequest<CampaignRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CampaignId;
}
