using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The held report use ended after the recorded review or after the cancellation fence. With no earlier grant it
///     fences the key so a delayed acquisition cannot recreate the hold.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-settled", 1)]
public sealed record ProviderAssuranceReportProviderReviewUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
