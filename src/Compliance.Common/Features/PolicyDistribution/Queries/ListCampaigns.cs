using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Queries;

[Discriminator("bdgrz.campaigns.list", 1)]
public sealed record ListCampaigns(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<CampaignSummaryView>>, IProgramReadRequest, ICallable;
