using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The pending review was canceled before it was recorded, fenced by a proven permanent source mismatch.
///     <paramref name="SourceProofs" /> is the failure proof; a recorded review is never canceled.
/// </summary>
[Discriminator("bdgrz.provider-assurance-review.provider-review-canceled", 1)]
public sealed record ProviderReviewCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
