using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Runs a command against its bounded finding owner after the recorded cutover.</summary>
static class RemediationCommands
{
    public static async ValueTask<Result<FindingView>> ExecuteAsync<TRequest>(
        IAggregateExecutor executor, IAggregateReader reader, IDomainEventReader events,
        IRequestContext<TRequest> context, Uuid tenantId,
        Uuid programId, Uuid findingId, DateTimeOffset now,
        Func<FindingAggregate, FindingHistoryFold, CommandFailure?> command, CancellationToken ct,
        Func<CancellationToken, ValueTask<RequestError?>>? validateNewCommand = null)
        where TRequest : IRequestBase
    {
        var selected = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId,
            context.RequestId), ct).ConfigureAwait(false);
        if (selected.HasSelectedReceipt)
        {
            var failure = command(selected, new FindingHistoryFold(selected.Version, [], [], []));
            return failure is not null ? Result<FindingView>.Failure(failure.ToRequestError())
                : await new FindingReadSource(reader, events).ReadOneAsync(tenantId, programId, findingId, now, ct)
                    .ConfigureAwait(false);
        }
        if (validateNewCommand is not null && await validateNewCommand(ct).ConfigureAwait(false) is { } invalid)
            return Result<FindingView>.Failure(invalid);
        var ready = await FindingStreamRouting.EnsureReadyAsync(executor, reader, events, context,
            tenantId, programId, now, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<FindingView>.Failure(ready.Error);
        var history = await FindingHistoryFold.ReadAsync(events, tenantId, programId, findingId, ct)
            .ConfigureAwait(false);
        var result = await executor.ExecuteAsync(new FindingAggregate(tenantId, programId, findingId, context.RequestId), finding =>
        {
            var failure = (finding.HasSelectedReceipt ? null : finding.CheckHistoryVersion(history.StreamVersion)) ?? command(finding, history);
            return CommandFailureRequestAdapter.ToOutcome(failure);
        }, context, ct).ConfigureAwait(false);
        return !result.IsSuccess ? Result<FindingView>.Failure(result.Error)
            : await new FindingReadSource(reader, events).ReadOneAsync(tenantId, programId, findingId, now, ct)
                .ConfigureAwait(false);
    }

    public static async ValueTask<bool> IsActiveMemberAsync(IAggregateReader reader,
        Uuid tenantId, Uuid memberId, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && member.Affiliation != "firm_staff";
    }

    public static RequestError InactiveOwner() => new(RequestErrorKind.Validation,
        "The owner must be an active client member.");
}
