using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Records the acting member's personal approval of a missing-population exception as its own owner; HTTP-only.
///     The exception id is the logical operation identity and the owner identity, so an exact retry returns the
///     original frozen exception and unrelated exceptions never contend.
/// </summary>
public sealed class ExemptMissingAccessPopulationHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ExemptMissingAccessPopulation, AccessPopulationExceptionView>
{
    const string CommandKind = "ExemptMissingAccessPopulation";

    public async ValueTask<Result<AccessPopulationExceptionView>> HandleAsync(
        IRequestContext<ExemptMissingAccessPopulation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(RequestErrorKind.Forbidden,
                "Access review sign-off requires personal HTTP submission.");
        var request = context.Request;
        if (request.ExceptionId == Uuid.Empty || request.ExpectedRevision < 0)
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(RequestErrorKind.Validation,
                "A population exception requires its immutable exception id and a non-negative expected revision.");
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events, request.TenantId,
            request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (instance is null)
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                instance.ApplicationId, instance.Id, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(
                RequestErrorKind.NotFound, "The system instance was not found.");

        var now = clock.GetUtcNow();
        var stream = new AccessPopulationException(request.TenantId, request.ExceptionId).Stream;
        var provenance = AccessReviewOperation.Provenance(request.ExceptionId, CommandKind, stream, actor.Reference,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new AccessPopulationException(request.TenantId,
            request.ExceptionId, request.ExceptionId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? exceptionContent = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareRecord(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<AccessPopulationExceptionView>.Failure(prepared.Error!);
            if (!AccessReviewVocabulary.IsBoundedText(request.Reason, 4000))
                return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(RequestErrorKind.Validation,
                    "A population exception requires a reason of at most 4000 characters.");
            if (request.ExpiresAt <= now)
                return AccessReviewOutcome.Failure<AccessPopulationExceptionView>(RequestErrorKind.Validation,
                    "A population exception must expire in the future.");
            var view = new AccessPopulationExceptionView(request.ExceptionId, instance.Id, request.Reason.Trim(),
                request.ExpiresAt, actor.Reference, now);
            exceptionContent = await AccessReviewContent.WriteAsync(content, request.TenantId,
                AccessReviewContent.PopulationExceptionSchema, view, ct).ConfigureAwait(false);
            result = await AccessReviewContent.WriteResultAsync(content, request.TenantId,
                AccessReviewContent.PopulationExceptionSchema, exceptionContent, ct).ConfigureAwait(false);
            response = AccessReviewContent.OkResponse(AccessReviewContent.PopulationExceptionSchema,
                exceptionContent);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AccessPopulationException>(binding,
            () => new AccessPopulationException(request.TenantId, request.ExceptionId, request.ExceptionId),
            exception => exception.Acceptance,
            exception => AccessReviewOperation.Prepare(exception.PrepareRecord(request.ExpectedRevision), response,
                "The population exception changed while it was prepared; reload and retry."),
            (AccessPopulationException exception, ImmutableContentRef frozen) => exception.Record(provenance, request.SystemInstanceId, exceptionContent!, frozen,
                result!)), context, ct).ConfigureAwait(false);
        return await AccessReviewContent.ResponseAsync<AccessPopulationExceptionView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }
}
