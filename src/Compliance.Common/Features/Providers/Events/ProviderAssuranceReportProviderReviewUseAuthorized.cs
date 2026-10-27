using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The exact assurance report revision is held for one provider review. The use does not advance the public
///     report revision; <paramref name="Revision" /> is the held revision and the report refuses revision while held.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.provider-assurance-report-provider-review-use-authorized", 1)]
public sealed record ProviderAssuranceReportProviderReviewUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
