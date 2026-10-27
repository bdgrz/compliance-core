using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Compares a later frozen roster snapshot with the campaign population and records joiner,
///     mover, and leaver amendments. It never changes platform or provider access.
///     <paramref name="ExpectedRevision" /> is the campaign's logical revision.
/// </summary>
[Discriminator("bdgrz.campaign.audience.reconcile", 2)]
public sealed record ReconcileCampaignAudience(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid RosterSnapshotId, long ExpectedRevision)
    : IRequest<CampaignReconciliationView>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
