using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The report pair of the generic settle receiver. The terminal fact is resolved from the review owner: a
///     recording requires <c>irreversible_decision</c>, a cancellation requires <c>canceled</c>. A settlement before
///     any grant fences the key; a missing terminal fact is a transient wait, never an inferred cancellation.
/// </summary>
sealed class SettleProviderAssuranceReportProviderReviewUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock, ProviderReviewVerifier verifier)
    : SettleSourceUseHandler<ProviderAssuranceReport, SettleProviderAssuranceReportProviderReviewUse>(reader, executor,
        content, clock)
{
    protected override ProviderAssuranceReport Open(SettleProviderAssuranceReportProviderReviewUse request,
        Uuid selectedCommandId) =>
        new(ProviderReviewEffects.TenantOf(request.Target), request.ProviderId, request.SourceOwnerId,
            selectedCommandId);

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleProviderAssuranceReportProviderReviewUse request, CancellationToken ct) =>
        verifier.SettleSourceAsync(request, ct);

    protected override Func<ProviderAssuranceReport, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (ProviderAssuranceReport report, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            report.SettleProviderReviewUse(provenance, intent, generation);
}
