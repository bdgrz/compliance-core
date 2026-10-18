using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Persists a delivery attempt only for the exact scheduled digest and current recipient.</summary>
public sealed class DeliverWeeklyWorkDigestHandler(IAggregateReader reader,
    IAggregateExecutor executor, IDomainEventReader events,
    WeeklyWorkDigestRecipientResolver recipients, TimeProvider clock)
    : IRequestHandler<DeliverWeeklyWorkDigest>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DeliverWeeklyWorkDigest> context,
        CancellationToken ct)
    {
        if (!WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId))
            return Forbidden();

        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty ||
            request.DigestId == Uuid.Empty || request.ScheduleEventId == Uuid.Empty ||
            request.ExpectedRevision <= 0 || request.AttemptNumber <= 0 ||
            context.CausationId != request.ScheduleEventId ||
            request.DeliveryKey != WeeklyWorkDigest.DeliveryKeyFor(request.DigestId,
                request.AttemptNumber))
            return InvalidOrWrongCause(context, request);

        if (!await IsExactScheduleAsync(request, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The delivery request must be caused by its exact persisted weekly schedule event."));

        var digest = await reader.HydrateAsync(new WeeklyWorkDigest(request.TenantId,
            request.DigestId), ct).ConfigureAwait(false);
        if (digest.Revision == 0 || digest.MemberId != request.MemberId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The scheduled weekly digest was not found for this tenant member."));

        var actor = ActorReference.ForSystemProcess(WeeklyWorkDigest.DeliveryProcessId,
            "Weekly work digest delivery worker");
        var existingAttempt = digest.DeliveryAttempts.Any(attempt =>
            attempt.Number == request.AttemptNumber && attempt.DeliveryKey == request.DeliveryKey);
        var suppression = existingAttempt
            ? null
            : await recipients.SuppressionReasonAsync(request.TenantId, request.MemberId, ct)
                .ConfigureAwait(false);
        return await executor.ExecuteAsync(new WeeklyWorkDigest(request.TenantId,
                request.DigestId), current =>
            {
                var result = existingAttempt || suppression is null
                    ? current.RequestDelivery(request.ExpectedRevision, request.AttemptNumber,
                        request.DeliveryKey, actor, clock.GetUtcNow())
                    : current.SuppressDelivery(request.ExpectedRevision, suppression, actor,
                        clock.GetUtcNow());
                return AggregateOutcome.CommitOnSuccess(result);
            }, context, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> IsExactScheduleAsync(DeliverWeeklyWorkDigest request, CancellationToken ct)
    {
        var stream = new WeeklyWorkDigest(request.TenantId, request.DigestId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, ct).WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (record.Event.Metadata.EventId != request.ScheduleEventId)
                continue;
            return record.Stream == stream && record.Event is WeeklyWorkDigestScheduled scheduled &&
                   scheduled.TenantId == request.TenantId && scheduled.DigestId == request.DigestId &&
                   scheduled.MemberId == request.MemberId &&
                   WeeklyWorkDigest.IdFor(request.TenantId, request.MemberId,
                       scheduled.WeekStarting) == request.DigestId;
        }
        return false;
    }

    static Result InvalidOrWrongCause(IRequestContext<DeliverWeeklyWorkDigest> context,
        DeliverWeeklyWorkDigest request) =>
        Result.Failure(new RequestError(context.CausationId != request.ScheduleEventId
                ? RequestErrorKind.Conflict
                : RequestErrorKind.Validation,
            "A weekly digest delivery requires its exact schedule, member, attempt, and deterministic key."));

    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only the named weekly digest delivery worker may request an email attempt."));
}
