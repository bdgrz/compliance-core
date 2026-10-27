using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The held report use ended after the recorded review or after the cancellation fence. With no earlier grant it
///     fences the key so a delayed acquisition cannot recreate the hold.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled", 1)]
public sealed record ProviderAssuranceReportProviderReviewUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
