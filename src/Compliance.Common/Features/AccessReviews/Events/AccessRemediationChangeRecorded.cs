using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>A provider-side change was reported; the complete change is verified content.</summary>
[Discriminator("bdgrz.access_review.remediation.change_recorded", 2)]
public sealed record AccessRemediationChangeRecorded(Uuid TenantId, Uuid CampaignId, long Revision, Uuid ItemId,
    ImmutableContentRef ChangeContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
