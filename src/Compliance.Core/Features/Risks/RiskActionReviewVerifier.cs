using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Verifies a receiver's proven source: the frozen review intent and authored decision are read from verified
///     immutable content, and the accepted review fact (or the terminal fact that ended it) is found on the
///     authoritative treatment action. The source reference only names the fact; nothing the caller states is trusted
///     until the action confirms it.
/// </summary>
sealed class RiskActionReviewVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Hydrates an owner from the authoritative streams.</summary>
    public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : Aggregate => reader.HydrateAsync(aggregate, ct);

    /// <summary>Verifies the accepted review named by <paramref name="source" />.</summary>
    public async ValueTask<RiskActionReview> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var review = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (review.Error is not null)
            return review;
        var action = review.Action!;
        if (action.AcceptedReview(operationId) is not { } accepted)
            return RiskActionReview.Failed(Transient("The accepted review is not visible yet."));
        return source.Stream != action.Stream || accepted.Metadata.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference
            ? RiskActionReview.Failed(Permanent("The source is not the accepted review of this action."))
            : review;
    }

    /// <summary>Verifies the terminal fact (decision or cancellation) named by <paramref name="source" />.</summary>
    public async ValueTask<RiskActionReview> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var review = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (review.Error is not null)
            return review;
        var action = review.Action!;
        if (action.TerminalReview(operationId) is not { } terminal)
            return RiskActionReview.Failed(Transient("Neither the review decision nor its cancellation is visible yet."));
        var intent = terminal switch
        {
            RiskTreatmentActionCompletionReviewed reviewed => reviewed.Intent,
            RiskTreatmentActionCompletionReviewCanceled canceled => canceled.Intent,
            _ => null,
        };
        return source.Stream != action.Stream || terminal.Metadata.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion || intent != intentReference
            ? RiskActionReview.Failed(Permanent("The source is not the terminal fact of this review."))
            : review;
    }

    async ValueTask<RiskActionReview> ReadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return RiskActionReview.Failed(Permanent("The review source is not a tenant treatment action stream."));
        RiskActionCompletionReviewIntent intent;
        RiskActionReviewDecision decision;
        try
        {
            intent = await FindingContent.ReadAsync<RiskActionCompletionReviewIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<RiskActionReviewDecision>(content, tenant, intent.AuthoredReview,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return RiskActionReview.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The frozen review cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        if (intent.Header is null || intent.Header.RootOperationId != operationId || intent.ActionId == Uuid.Empty ||
            decision.ProgramId == Uuid.Empty || decision.RiskId == Uuid.Empty || decision.DecisionId != operationId)
            return RiskActionReview.Failed(Permanent("The command differs from the frozen review intent."));
        var action = await reader.HydrateAsync(new RiskTreatmentAction(tenant, decision.ProgramId, decision.RiskId,
            intent.ActionId, commandId, operationId), ct).ConfigureAwait(false);
        if (action.Stream != source.Stream)
            return RiskActionReview.Failed(Permanent("The frozen review belongs to another exact action stream."));
        if (action.AcceptedReview(operationId) is { } accepted)
        {
            var operation = accepted.Operation;
            var header = intent.Header;
            var submission = action.SubmissionForReview(operationId);
            if (header.FirstOwner != action.Stream || header.AffectedOwner != action.Stream ||
                header.ExpectedRevisionOwner != action.Stream || header.OriginalActor != operation.Actor ||
                header.Episode != operation.Episode || header.OriginalExpectedRevision != operation.OriginalExpectedRevision ||
                header.AuthoredIntentSha256 != operation.AuthoredIntentSha256 || header.AcceptedAt != operation.AcceptedAt ||
                submission is null || submission.SubmissionId != intent.SubmissionId ||
                decision.Reviewer != operation.Actor || decision.DecidedAt != operation.AcceptedAt ||
                decision.ReviewerMemberId.ToString() != operation.Actor.Id)
                return RiskActionReview.Failed(Permanent("The sealed review differs from its exact admitted submission or original authority."));
        }
        return new RiskActionReview(null, action, intent, decision, tenant);
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
