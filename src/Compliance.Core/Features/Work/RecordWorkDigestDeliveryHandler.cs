using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records an outcome only for the exact requested attempt on the digest stream.</summary>
public sealed class RecordWorkDigestDeliveryHandler(IAggregateExecutor executor,
    IDomainEventReader events, TimeProvider clock) : IRequestHandler<RecordWorkDigestDelivery>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordWorkDigestDelivery> context,
        CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result> HandleCoreAsync(IRequestContext<RecordWorkDigestDelivery> context,
        CancellationToken ct)
    {
        if (!WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId))
            return Forbidden();

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.DigestId == Uuid.Empty ||
            request.DeliveryRequestEventId == Uuid.Empty || request.ExpectedRevision <= 1 ||
            request.AttemptNumber <= 0 || request.DeliveryKey != WeeklyWorkDigest.DeliveryKeyFor(
                request.DigestId, request.AttemptNumber) || request.AttemptedAt == default ||
            request.AttemptedAt > clock.GetUtcNow())
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A weekly digest result requires its exact attempt, deterministic key, and valid completion time."));
        if (context.CausationId != request.DeliveryRequestEventId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The result must be caused by the exact weekly digest delivery request."));
        if (!await IsExactRequestAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The delivery request event does not match this exact weekly digest attempt."));

        var actor = ActorReference.ForSystemProcess(WeeklyWorkDigest.DeliveryProcessId,
            "Weekly work digest delivery worker");
        return await executor.ExecuteAsync(new WeeklyWorkDigest(request.TenantId,
                request.DigestId, request.AttemptNumber), digest =>
            {
                return AggregateOutcome.CommitOnSuccess(digest.RecordDeliveryResult(
                    request.ExpectedRevision, request.AttemptNumber, request.DeliveryKey,
                    request.Outcome, request.FailureCode, actor, request.AttemptedAt));
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactRequestAsync(RecordWorkDigestDelivery request,
        CancellationToken ct)
    {
        var stream = new WeeklyWorkDigest(request.TenantId, request.DigestId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.DeliveryRequestEventId)
                continue;
            return record.Stream == stream &&
                   record.Event is WeeklyWorkDigestDeliveryRequested requested &&
                   requested.TenantId == request.TenantId && requested.DigestId == request.DigestId &&
                   requested.Revision == request.ExpectedRevision &&
                   requested.AttemptNumber == request.AttemptNumber &&
                   requested.DeliveryKey == request.DeliveryKey &&
                   requested.RequestedBy is { Kind: "system_process", Id: WeeklyWorkDigest.DeliveryProcessId };
        }
        return false;
    }

    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only the named weekly digest delivery worker may record email outcomes."));
}
