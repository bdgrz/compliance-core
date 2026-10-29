using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     A personal provider review was admitted as the root fact of its owner. <paramref name="Intent" /> is the
///     frozen <see cref="ProviderReviewIntent" />; nothing about the review is recorded by admission alone.
/// </summary>
[Discriminator("bdgrz.provider-assurance-review.provider-review-requested", 1)]
public sealed record ProviderReviewRequested(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
