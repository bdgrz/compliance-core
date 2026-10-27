using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     The Compliance Lead's personal approval of a bounded exception of at most 12 months.
///     HTTP-only. An excepted person never counts as acknowledged. The campaign admits the complete result first
///     (<paramref name="ExpectedCampaignRevision" /> is the campaign's logical revision).
/// </summary>
[Discriminator("bdgrz.campaign.waiver.approve", 2)]
public sealed record ApproveCampaignWaiver(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, string Reason, DateOnly ExpiresOn, long ExpectedCampaignRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
