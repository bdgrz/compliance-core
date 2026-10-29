using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>A campaign responsibility was changed after its launch-frozen owner lost eligibility.</summary>
[Discriminator("bdgrz.access_review.responsibility.reassigned", 2)]
public sealed record AccessReviewResponsibilityReassigned(Uuid TenantId, Uuid CampaignId, Uuid ItemId, long Revision,
    ImmutableContentRef ReassignmentContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
