using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records the acting member's personal approval of an expiring exception to an approved expectation on its line;
///     HTTP-only. The command id is both the logical operation identity and the exception id, so an exact retry
///     returns the original frozen exception. An exception never authorizes a successor expectation.
/// </summary>
public sealed class ExemptAccessExpectationHandler(NativeOperationExecutor operations,
    AccessExpectationsReader expectations, IDomainEventReader events, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptAccessExpectation, AccessExpectationExceptionView>
{
    const string CommandKind = "ExemptAccessExpectation";

    public async ValueTask<Result<AccessExpectationExceptionView>> HandleAsync(
        IRequestContext<ExemptAccessExpectation> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessExpectationExceptionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact scoped expectation source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessExpectationExceptionView>> HandleCoreAsync(
        IRequestContext<ExemptAccessExpectation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(RequestErrorKind.Validation,
                "An exception requires a command id and a non-negative expected revision.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                request.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        var digest = AuthoredIntentDigest.Compute(request);
        var lineId = await expectations.ResolveLineAsync(request.TenantId, request.SystemInstanceId,
            request.ExpectationId, request.CommandId, CommandKind, digest, ct).ConfigureAwait(false);
        if (lineId is not { } line)
            return AccessReviewOutcome.Failure<AccessExpectationExceptionView>(RequestErrorKind.NotFound,
                "The approved expectation was not found.");

        var now = clock.GetUtcNow();
        var stream = new AccessExpectation(request.TenantId, line).Stream;
        var provenance = AccessReviewOperation.Provenance(request.CommandId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, digest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        AccessReviewPreparedCut? preparedCut = null;
        ImmutableContentRef? exceptionContent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessExpectation>(binding,
            () => new AccessExpectation(request.TenantId, line, request.CommandId, request.ExpectationId),
            expectation => expectation.Acceptance,
            async (owner, prepareCt) =>
            {
                _ = await AccessExpectationHistory.ReadAsync(events, owner, prepareCt).ConfigureAwait(false);
                var prepared = owner.PrepareException(request.ExpectedRevision, request.ExpectationId);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (!AccessReviewVocabulary.IsBoundedText(request.ProviderSubjectId, 512) ||
                    request.ProviderEntitlementId is { Length: 0 or > 512 } ||
                    !AccessReviewVocabulary.IsBoundedText(request.Rationale, 4000))
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "An exception requires a principal, an optional entitlement, and a rationale of at most 4000 characters.");
                if (request.ExpiresAt <= now)
                    return AccessReviewOutcome.Failure<FirstResponseDescriptor>(RequestErrorKind.Validation,
                        "An exception must expire in the future.");
                var view = new AccessExpectationExceptionView(request.CommandId, request.ExpectationId,
                    request.ProviderSubjectId, request.ProviderEntitlementId, request.Rationale.Trim(),
                    request.ExpiresAt, actor.Reference, now);
                exceptionContent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                    AccessReviewContent.ExpectationExceptionSchema, view, prepareCt).ConfigureAwait(false);
                result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                    AccessReviewContent.ExpectationExceptionSchema, exceptionContent, prepareCt).ConfigureAwait(false);
                response = AccessReviewContent.OkResponse(AccessReviewContent.ExpectationExceptionSchema,
                    exceptionContent);
                preparedCut = AccessReviewPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AccessExpectation expectation, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(expectation) != true)
                    return AccessReviewPreparedCut.Changed();
                return expectation.RecordException(provenance, request.ExpectationId,
                exceptionContent!, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessExpectationExceptionView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
