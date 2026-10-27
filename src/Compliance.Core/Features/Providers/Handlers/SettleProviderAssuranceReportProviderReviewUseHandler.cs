using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

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
