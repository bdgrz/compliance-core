using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     The report pair of the generic acquire receiver: it holds the exact report revision a review pinned at
///     admission. A report that was revised since rejects the review, and the abort path cancels it and fences the
///     key so a delayed acquisition cannot recreate the hold.
/// </summary>
sealed class AuthorizeProviderAssuranceReportProviderReviewUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock, ProviderReviewVerifier verifier)
    : AuthorizeSourceUseHandler<ProviderAssuranceReport, AuthorizeProviderAssuranceReportProviderReviewUse>(reader,
        executor, content, clock)
{
    protected override ProviderAssuranceReport Open(AuthorizeProviderAssuranceReportProviderReviewUse request,
        Uuid selectedCommandId) =>
        new(ProviderReviewEffects.TenantOf(request.Target), request.ProviderId, request.SourceOwnerId,
            selectedCommandId);

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeProviderAssuranceReportProviderReviewUse request, CancellationToken ct) =>
        verifier.AuthorizeSourceAsync(request, ct);

    protected override ValueTask<Result> VerifyPinnedAsync(ProviderAssuranceReport owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (!owner.Exists)
            return ValueTask.FromResult(Result.Failure(ProviderReviewEffects.Transient(
                "The assurance report is not visible yet.")));
        return ValueTask.FromResult(IsPinned(owner, requirement)
            ? Result.Success
            : Result.Failure(ProviderReviewEffects.Permanent(
                "The assurance report was revised after the review was admitted.")));
    }

    protected override bool IsPinned(ProviderAssuranceReport owner, SourceUseRequirement requirement) =>
        owner.LatestFact is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<ProviderAssuranceReport, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (ProviderAssuranceReport report, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            report.AuthorizeProviderReviewUse(provenance, intent, generation);

    protected override ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeProviderAssuranceReportProviderReviewUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct) => ValueTask.FromResult(
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason, null)));
}
