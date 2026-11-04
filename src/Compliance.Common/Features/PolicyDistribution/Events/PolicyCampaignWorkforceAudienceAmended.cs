using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>A source-attributed workforce amendment; the frozen launch roster remains unchanged.</summary>
[Discriminator("bdgrz.policy-campaign.workforce-audience-amended", 1)]
public sealed record PolicyCampaignWorkforceAudienceAmended(Uuid TenantId, Uuid CampaignId, long Revision,
    long Reconciliation, NativeFactReference WorkforceFact, CampaignAudienceAmendment Amendment,
    ActorReference Actor, DateTimeOffset AmendedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
