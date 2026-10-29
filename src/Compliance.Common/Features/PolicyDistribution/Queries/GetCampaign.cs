using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Queries;

/// <summary>Reads a campaign's aggregate result as of a date (default today).</summary>
[Discriminator("bdgrz.campaign.get", 1)]
public sealed record GetCampaign(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    DateOnly? AsOf = null)
    : IRequest<CampaignView>, IProgramReadRequest, ICallable;
