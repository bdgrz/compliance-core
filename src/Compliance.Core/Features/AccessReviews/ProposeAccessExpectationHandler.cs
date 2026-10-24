using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Proposes an expectation on its expectation line. A root expectation opens a new line (expected revision zero);
///     a successor joins the line of its approved predecessor, so a predecessor and its successors share one conflict
///     key and two successors racing one predecessor serialize on the line revision. The expectation id is the
///     logical operation identity, so an exact retry returns the original frozen view even after later changes.
/// </summary>
public sealed class ProposeAccessExpectationHandler(NativeOperationExecutor operations,
    AccessExpectationsReader expectations, IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ProposeAccessExpectation, AccessExpectationView>
{
    const string CommandKind = "ProposeAccessExpectation";

    public async ValueTask<Result<AccessExpectationView>> HandleAsync(
        IRequestContext<ProposeAccessExpectation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectationId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation,
                "An expectation requires its immutable expectation id and a non-negative expected revision.");
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                instance.ApplicationId, instance.Id, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The system instance was not found.");

        var lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId,
            request.ExpectationId, ct).ConfigureAwait(false);
        if (lineId is null && request.SupersedesExpectationId is { } predecessorId)
            lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId, predecessorId,
                ct).ConfigureAwait(false) ?? Uuid.Empty;
        if (lineId == Uuid.Empty)
            return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                "The superseded expectation was not found or is not approved.");
        var line = lineId ?? request.ExpectationId;

        var now = clock.GetUtcNow();
        var stream = new AccessExpectation(request.TenantId, line).Stream;
        var provenance = AccessReviewOperation.Provenance(request.ExpectationId, CommandKind, stream,
            actor.Reference, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var (owner, folded) = await expectations.ReadLineAsync(request.TenantId, line, request.ExpectationId, ct)
            .ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? expectationContent = null;
        ImmutableContentRef? result = null;
        if (owner.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = owner.PrepareProposal(request.ExpectedRevision, request.SystemInstanceId,
                request.ExpectationId);
            if (!prepared.IsSuccess)
                return Result<AccessExpectationView>.Failure(prepared.Error!);
            if (AccessExpectationRules.ValidateRule(request.RuleKind, request.Parameters) is { } invalid)
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation, invalid);
            if (!AccessReviewVocabulary.IsBoundedText(request.Rationale, 4000))
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation,
                    "An expectation requires a rationale of at most 4000 characters.");
            if (request.EffectiveUntil is { } until && until <= request.EffectiveFrom)
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.Validation,
                    "An expectation must end after it takes effect.");
            if (request.SupersedesExpectationId is { } supersedes &&
                folded.Find(supersedes) is not { Status: AccessExpectation.Approved })
                return AccessReviewOutcome.Failure<AccessExpectationView>(RequestErrorKind.NotFound,
                    "The superseded expectation was not found or is not approved.");
            var view = new AccessExpectationView(request.ExpectationId, instance.Id, request.SupersedesExpectationId,
                request.RuleKind, AccessExpectationRules.Normalize(request.Parameters), request.Rationale.Trim(),
                request.EffectiveFrom, request.EffectiveUntil, AccessExpectation.Proposed, actor.Reference, now,
                null, null, null, request.ExpectedRevision + 1);
            expectationContent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.ExpectationSchema, view, ct).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.ExpectationSchema, expectationContent, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.ExpectationSchema, expectationContent);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessExpectation>(binding,
            () => new AccessExpectation(request.TenantId, line, request.ExpectationId),
            expectation => expectation.Acceptance,
            expectation => AccessReviewOperation.Prepare(expectation.PrepareProposal(request.ExpectedRevision,
                request.SystemInstanceId, request.ExpectationId), response,
                "The expectation line changed while the proposal was prepared; reload and retry."),
            (AccessExpectation expectation, ImmutableContentRef frozen) => expectation.Propose(provenance, request.SystemInstanceId, actor.MemberId,
                expectationContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessExpectationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
