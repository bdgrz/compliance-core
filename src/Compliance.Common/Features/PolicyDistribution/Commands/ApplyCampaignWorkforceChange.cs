using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Contracts;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>Internal source-bound reconciliation of one person's inclusion in one campaign.</summary>
[Discriminator("bdgrz.policy-campaign.workforce-change.apply", 1)]
public sealed record ApplyCampaignWorkforceChange(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    NativeFactReference Source) : IRequest, IPolicyCampaignReceiver;
