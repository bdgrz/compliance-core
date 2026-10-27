using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Records the admitted review as an immutable fact once the exact report revision it relies on is held. The
///     source is verified from the review owner, each required grant is verified from the report owner, and the
///     provider revision and materiality are observed as historical facts of the recording. An exact retry replays
///     the original acceptance; a canceled review makes the receiver a terminal no-op.
/// </summary>
sealed class CompleteProviderAssuranceReviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, AssuranceReferences references, ProviderReviewVerifier verifier,
    TimeProvider clock) : IRequestHandler<CompleteProviderAssuranceReview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteProviderAssuranceReview> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationId == Uuid.Empty || request.ReviewId != request.OperationId ||
            request.Generation != ProviderReviewEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The completion names another review or generation.");
        var loaded = await verifier.LoadAsync(request.Source, request.Intent, request.OperationId, false, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (review, intent, requirements) = (loaded.Owner!, loaded.Intent!, loaded.Requirements!);
        var tenantId = ProviderReviewEffects.TenantOf(review.Stream);
        if (request.Target != review.Stream)
            return SourceUseReceiverSupport.Permanent("The completion names another target stream.");
        var provenance = SourceUseReceiverSupport.Child(request.CommandId, nameof(CompleteProviderAssuranceReview),
            request.Source.EventId, intent.Header, review.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(new ProviderAssuranceReview(tenantId, request.ReviewId,
                    request.CommandId), ct).ConfigureAwait(false);
                if (Decide(owner, binding) is { } early)
                    return early;
                var held = await VerifyUsesAsync(request, tenantId, intent, requirements, ct).ConfigureAwait(false);
                if (!held.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(held.Error!);
                var provider = await references.ProviderAsync(tenantId, intent.ProviderId, ct).ConfigureAwait(false);
                if (!provider.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(provider.Error);
                RecordProviderReview authored;
                try
                {
                    authored = await ProviderAssuranceContent.ReadAsync<RecordProviderReview>(content, tenantId,
                        intent.AuthoredReview, ct).ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return SourceUseReceiverSupport.Permanent($"The authored review cannot be verified: {exception.Message}");
                }
                var normalized = AssuranceRules.Normalize(authored.Content);
                var reviewContent = await ProviderAssuranceContent.WriteAsync(content, tenantId,
                    ProviderAssuranceContent.ReviewRecordedSchema, normalized, ct,
                    Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.ReviewRecordedSchema))
                    .ConfigureAwait(false);
                var result = await ProviderAssuranceContent.WriteAsync(content, tenantId,
                    ProviderAssuranceContent.ReviewRegistrationSchema, new ProviderReviewRegistration(request.ReviewId),
                    ct, Uuid.CreateVersion5(request.CommandId, ProviderAssuranceContent.ReviewRegistrationSchema))
                    .ConfigureAwait(false);
                long? reportRevision = requirements.Count == 0 ? null : (long)requirements[0].ResourceRevision;
                var step = await executor.ExecuteAsync(new ProviderAssuranceReview(tenantId, request.ReviewId,
                    request.CommandId), fresh =>
                {
                    if (Decide(fresh, binding) is { } decided)
                        return AggregateOutcome.Discard(Result<Decision>.Success(new Decision(decided)));
                    var raised = fresh.Complete(provenance, reviewContent, provider.Value.Revision,
                        provider.Value.Content.Materiality, reportRevision, intent.Header.OriginalActor,
                        intent.ReviewedAt, intent.ProviderId, result);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Decision>.Success(new Decision(
                            SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Decision>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The review changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>Every required report grant must be the exact active grant the report owner holds for its key.</summary>
    async ValueTask<Result> VerifyUsesAsync(CompleteProviderAssuranceReview request, Uuid tenantId,
        ProviderReviewIntent intent, IReadOnlyList<SourceUseRequirement> requirements, CancellationToken ct)
    {
        NativeFactReference[] grants;
        try
        {
            grants = await ProviderAssuranceContent.ReadAsync<NativeFactReference[]>(content, tenantId,
                request.ReportUseProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(ProviderReviewEffects.Permanent(
                $"The report use proof cannot be verified: {exception.Message}"));
        }
        if (grants.Length != requirements.Count)
            return Result.Failure(ProviderReviewEffects.Permanent("The report use proof differs from the required uses."));
        if (requirements.Count == 0)
            return Result.Success;
        RecordProviderReview authored;
        try
        {
            authored = await ProviderAssuranceContent.ReadAsync<RecordProviderReview>(content, tenantId,
                intent.AuthoredReview, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(ProviderReviewEffects.Permanent(
                $"The authored review cannot be verified: {exception.Message}"));
        }
        for (var index = 0; index < requirements.Count; index++)
        {
            var requirement = requirements[index];
            var report = await reader.HydrateAsync(new ProviderAssuranceReport(tenantId, intent.ProviderId,
                authored.Content.AssuranceReportId ?? Uuid.Empty).ForSourceUse(candidate => ProviderReviewEffects.UseKey(request.OperationId, request.Target, requirement.SourceOwner)), ct).ConfigureAwait(false);
            var key = ProviderReviewEffects.UseKey(request.OperationId, request.Target, requirement.SourceOwner);
            var grant = report.Stream == requirement.SourceOwner ? report.Uses.GrantOf(key) : null;
            if (grant is null || grant.GrantEvent != grants[index])
                return Result.Failure(ProviderReviewEffects.Transient("The report use is not held yet."));
            if (report.Uses.Find(key) is not { IsActive: true })
                return Result.Failure(ProviderReviewEffects.Permanent("The report use was settled before the review was recorded."));
        }
        return Result.Success;
    }

    static Result<ReceiverOutcome>? Decide(ProviderAssuranceReview owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        return owner.Canceled is { } canceled
            ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The review was canceled.", NativeFactReference.Of(owner.Stream, canceled)))
            : owner.Recorded is { } recorded
                ? SourceUseReceiverSupport.Superseded("The review is already recorded.",
                    NativeFactReference.Of(owner.Stream, recorded))
                : null;
    }

    sealed record Decision(Result<ReceiverOutcome> Outcome);
}
