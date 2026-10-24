using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     The evidence-request pair of the generic acquire receiver: it holds the exact fulfilled request a risk action
///     completion review pinned. A request that is not opened yet is a transient wait. A request that is not fulfilled,
///     is no longer exactly the pinned fulfilment, or is not cited by the frozen review aborts the review and fences
///     every use.
/// </summary>
sealed class AuthorizeEvidenceRequestActionCompletionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, RiskActionReviewVerifier verifier, RiskActionReviewFences fences)
    : AuthorizeSourceUseHandler<EvidenceRequest, AuthorizeEvidenceRequestActionCompletionUse>(reader, executor, content,
        clock)
{
    protected override EvidenceRequest Open(AuthorizeEvidenceRequestActionCompletionUse request,
        Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.ProgramId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeEvidenceRequestActionCompletionUse request, CancellationToken ct)
    {
        var review = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (review.Error is not null)
            return Result<SourceUseSource>.Failure(review.Error);
        var (action, intent, decision, tenant) = (review.Action!, review.Intent!, review.Decision!, review.TenantId);
        var accepted = action.AcceptedReview(request.OperationId)!;
        IReadOnlyList<ReviewedEvidenceRequest> cited;
        try
        {
            cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(Content, tenant.ToString(),
                intent.CompletionEvidence, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The cited evidence manifest cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        if (request.ReceivingOwnerId != intent.ActionId || request.ProgramId != decision.ProgramId ||
            request.Generation != accepted.Generation ||
            cited.All(item => item.EvidenceRequestId != request.SourceOwnerId))
            return Fail("The authorization differs from the frozen review evidence selection.", false);

        var terminal = action.TerminalReview(request.OperationId);
        var superseded = terminal switch
        {
            RiskTreatmentActionCompletionReviewed => "The review already decided the completion.",
            RiskTreatmentActionCompletionReviewCanceled => "The review was canceled.",
            _ => null,
        };
        var evidence = await verifier.HydrateAsync(Open(request, request.CommandId), ct).ConfigureAwait(false);
        var key = RiskActionReviewEffects.UseKey(intent, request.Generation, evidence.Stream);
        if (superseded is not null)
            return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key,
                Placeholder(evidence, intent), nameof(AuthorizeEvidenceRequestActionCompletionUse), null, superseded)
            {
                RequiresAbort = terminal is RiskTreatmentActionCompletionReviewCanceled,
            });
        if (!evidence.IsOpened)
            return Fail("The evidence request is not visible yet.", true);
        if (evidence.FactById(request.SourceContentEventId) is not { } pinned)
            return Fail("The pinned evidence request fact does not exist.", false);

        var fulfilled = evidence.FulfilledFact;
        var refusal = terminal is RiskTreatmentActionCompletionReviewCanceled ? "The review was canceled."
            : fulfilled is null ? "The evidence request is not fulfilled."
            : null;
        var facts = new ReviewEvidenceFacts(request.SourceOwnerId, fulfilled is not null, fulfilled?.ArtifactId, refusal);
        var factsReference = await FindingContent.WriteDeterministicAsync(Content, tenant, request.CommandId,
            RiskContent.EvidenceFactsSchema, facts, ct).ConfigureAwait(false);
        var requirement = new SourceUseRequirement(evidence.Stream, NativeFactReference.Of(evidence.Stream, pinned),
            request.ExpectedContentRevision, SourceUsePurpose.ActionCompletionReview, factsReference);
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key, requirement,
            nameof(AuthorizeEvidenceRequestActionCompletionUse), null, null));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(EvidenceRequest owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (!owner.IsOpened)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence request is not visible yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence request changed after the review pinned it.", false));
        ReviewEvidenceFacts facts;
        try
        {
            facts = await FindingContent.ReadAsync<ReviewEvidenceFacts>(Content,
                Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture),
                requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned evidence facts cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        return facts.Refusal is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, facts.Refusal, false));
    }

    protected override bool IsPinned(EvidenceRequest owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<EvidenceRequest, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (EvidenceRequest request, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            request.AuthorizeActionCompletionUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeEvidenceRequestActionCompletionUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var review = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return review.Error is not null
            ? Result<ReceiverOutcome>.Failure(review.Error)
            : await fences.AbortAsync(review, request.Source, request.Intent, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    /// <summary>A superseded acquisition is a no-op, so its requirement is never pinned or granted.</summary>
    static SourceUseRequirement Placeholder(EvidenceRequest request, RiskActionCompletionReviewIntent intent) =>
        new(request.Stream, new NativeFactReference(request.Stream, Uuid.Empty, 0), 0,
            SourceUsePurpose.ActionCompletionReview, intent.AuthoredReview);

    static Uuid Tenant(EventStreamAddress stream) =>
        Uuid.Parse(stream.Realm, System.Globalization.CultureInfo.InvariantCulture);

    static Result<SourceUseSource> Fail(string message, bool transient) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
