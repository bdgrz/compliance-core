using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Records the independent disclosure review of the exact submitted response version; the author of the version
///     cannot review it.
/// </summary>
sealed class ReviewAuditResponseHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviewAuditResponse>
{
    const string CommandKind = "ReviewAuditResponse";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviewAuditResponse> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var reviewerMemberId, out var reviewerDisplay))
            return AuditorCommands.Forbidden("Reviewing an audit response requires a personal HTTP actor.");
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out _))
            return AuditorCommands.Validation("The audit response version is invalid.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AuditorCommands.Validation(
                "A review requires a command id, a response and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(reviewerMemberId, reviewerDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? review = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        var outcomeToken = string.Empty;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareReview(request.ExpectedRevision, request.Version, reviewerMemberId);
            if (!prepared.IsSuccess)
                return prepared;
            if (AuditorRequestRules.Review(request.Outcome, request.Rationale) is not { } decision)
                return AuditorCommands.Validation("A response review needs an approved or rejected outcome and rationale.");
            outcomeToken = decision.Outcome;
            review = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.ReviewSchema,
                new AuditorResponseReviewView(decision.Outcome, decision.Rationale, reviewerMemberId, reviewerDisplay,
                    now), ct).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            auditorResponse => AuditorContent.Prepared(
                auditorResponse.PrepareReview(request.ExpectedRevision, request.Version, reviewerMemberId), response),
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) => auditorResponse.Review(provenance,
                request.Version, outcomeToken, review!, reviewerMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
