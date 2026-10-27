using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Closes a campaign and freezes its reconciled result. The campaign seals result admission, every admitted
///     participant result is applied, the participants' uses are held, and only then are the exact totals frozen.
///     <paramref name="ExpectedRevision" /> is the campaign's logical revision.
/// </summary>
[Discriminator("bdgrz.campaign.close", 2)]
public sealed record CloseCampaign(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    string Rationale, long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
