using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>A bounded exception that counts the person as excepted until it expires, applied to the participant owner.</summary>
[Discriminator("bdgrz.policy_campaign.waiver.approved", 2)]
public sealed record CampaignWaiverApproved(Uuid TenantId, Uuid CampaignId,
    Uuid WaiverId, Uuid PersonId, string Reason, DateOnly ExpiresOn,
    ActorReference Approver, Uuid ApproverMemberId, DateTimeOffset ApprovedAt, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
