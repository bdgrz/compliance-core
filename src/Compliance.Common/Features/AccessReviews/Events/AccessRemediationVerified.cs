using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>A later accepted population verified the remediation; the complete verification is verified content.</summary>
[Discriminator("bdgrz.access_review.remediation.verified", 2)]
public sealed record AccessRemediationVerified(Uuid TenantId, Uuid CampaignId, long Revision, Uuid ItemId,
    ImmutableContentRef VerificationContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
