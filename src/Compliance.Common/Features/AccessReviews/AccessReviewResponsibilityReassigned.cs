using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A campaign responsibility was changed after its launch-frozen owner lost eligibility.</summary>
[Discriminator("bdgrz.access_review.responsibility.reassigned", 2)]
public sealed record AccessReviewResponsibilityReassigned(Uuid TenantId, Uuid CampaignId, Uuid ItemId, long Revision,
    ImmutableContentRef ReassignmentContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
