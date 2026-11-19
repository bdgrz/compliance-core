using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Processes;

/// <summary>Runs the one live SMTP path against a member-week owner with current source checks before each send.</summary>
sealed class WeeklyWorkDigestProcessor(IAggregateReader reader, IAggregateExecutor executor,
    IWorkDigestContentReader content, IWorkDigestDelivery delivery, WorkDigestDeliverySettings settings, TimeProvider clock)
{
    public async ValueTask ProcessMemberAsync(Uuid tenantId, Uuid userId, bool tenantActive, bool memberActive, CancellationToken ct)
    {
        if (!settings.IsReady)
            return;
        var memberId = RbacIds.Member(tenantId, userId);
        var preference = await reader.HydrateAsync(new WorkDigestPreference(tenantId, memberId), ct).ConfigureAwait(false);
        if (!WorkDigestSchedule.TryGetDue(clock.GetUtcNow(), preference.Read().TimeZoneId, out var window))
            return;
        var owner = await ReadAsync(tenantId, memberId, window.WeekOf, ct).ConfigureAwait(false);
        if (owner.ReadDelivery().Status == "not_scheduled")
        {
            if (!await ChangeAsync(tenantId, memberId, window.WeekOf,
                    current => current.ScheduleDelivery(window, settings.RetryWindow, clock.GetUtcNow()), ct).ConfigureAwait(false))
                return;
            owner = await ReadAsync(tenantId, memberId, window.WeekOf, ct).ConfigureAwait(false);
        }
        var pending = owner.ReadDelivery();
        var now = clock.GetUtcNow();
        if (pending.Status is WorkDigestDispatch.Sent or WorkDigestDispatch.Skipped or WorkDigestDispatch.RetryExhausted or WorkDigestDispatch.PermanentFailure)
            return;
        if (pending.Status == WorkDigestDispatch.InFlight)
        {
            if (pending.LastAttemptAt is { } at && now >= at + settings.AttemptLease)
                _ = await ChangeAsync(tenantId, memberId, window.WeekOf, current => current.RecoverInterruptedDelivery(now), ct).ConfigureAwait(false);
            return;
        }
        if (pending.Status == WorkDigestDispatch.Unknown)
        {
            _ = await ChangeAsync(tenantId, memberId, window.WeekOf,
                current => current.RetryUnknownDelivery(now, settings.MaximumAttempts, settings.RetryDelay), ct).ConfigureAwait(false);
            return;
        }
        if (!WorkDigestSchedule.IsCurrentWeek(pending.WeekOf, pending.TimeZoneId, now))
        {
            _ = await ChangeAsync(tenantId, memberId, window.WeekOf, current => current.ExpireDelivery(now), ct).ConfigureAwait(false);
            return;
        }
        if (pending.NextAttemptAt is { } next && now < next || now < pending.ScheduledAt)
            return;
        if (!tenantActive || !memberActive)
        {
            _ = await ChangeAsync(tenantId, memberId, window.WeekOf, current => current.RecordWeeklySkipped("inactive_member", now), ct).ConfigureAwait(false);
            return;
        }
        var selected = await content.ReadAsync(tenantId, userId, pending, ct).ConfigureAwait(false);
        if (selected.Kind == WorkDigestContentReadKind.Unavailable)
            return;
        if (selected.Kind == WorkDigestContentReadKind.Skip)
        {
            _ = await ChangeAsync(tenantId, memberId, window.WeekOf,
                current => current.RecordWeeklySkipped(SkipReason(selected.SkipReason), clock.GetUtcNow()), ct).ConfigureAwait(false);
            return;
        }
        WorkDigestDispatchStatusView? claimed = null;
        if (!await ChangeAsync(tenantId, memberId, window.WeekOf, current =>
            {
                if (!current.StartDeliveryAttempt(clock.GetUtcNow()))
                    return false;
                claimed = current.ReadDelivery();
                return true;
            }, ct).ConfigureAwait(false) || claimed is null)
            return;
        // The claim may require a source append; recheck all business eligibility immediately before the external send.
        var final = await content.ReadAsync(tenantId, userId, claimed, ct).ConfigureAwait(false);
        if (final.Kind != WorkDigestContentReadKind.Ready)
        {
            _ = await ChangeClaimAsync(tenantId, memberId, window.WeekOf, claimed, current => final.Kind == WorkDigestContentReadKind.Skip
                ? current.RecordWeeklySkipped(SkipReason(final.SkipReason), clock.GetUtcNow())
                : current.RecordDeliveryUnknown(clock.GetUtcNow(), "source_unavailable_before_send"), ct).ConfigureAwait(false);
            return;
        }
        var message = final.Message ?? throw new InvalidOperationException("A ready weekly digest needs its exact current message.");
        if (message.MessageId != claimed.MessageId || message.ItemKeys is not { Count: > 0 and <= 200 })
            throw new InvalidOperationException("A weekly digest must retain its frozen message identity and bounded selected item keys.");
        WorkDigestTransportOutcome result;
        try
        {
            result = await delivery.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            result = new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown, "transport_ambiguous");
        }
        var recordedAt = clock.GetUtcNow();
        _ = await ChangeClaimAsync(tenantId, memberId, window.WeekOf, claimed, current => result.Kind switch
        {
            WorkDigestTransportOutcomeKind.Accepted => current.RecordWeeklySent(message, recordedAt),
            WorkDigestTransportOutcomeKind.DefiniteTransientRejection => current.RetryRejectedDelivery(recordedAt, settings.MaximumAttempts, settings.RetryDelay),
            WorkDigestTransportOutcomeKind.DefinitePermanentRejection => current.RecordRejectedDelivery(recordedAt, result.FailureCode ?? "smtp_rejected"),
            _ => current.RecordDeliveryUnknown(recordedAt, result.FailureCode ?? "transport_ambiguous")
        }, ct).ConfigureAwait(false);
    }

    ValueTask<WeeklyWorkDigest> ReadAsync(Uuid tenantId, Uuid memberId, DateOnly week, CancellationToken ct) =>
        reader.HydrateAsync(WeeklyWorkDigest.ForDelivery(tenantId, memberId, week), ct);

    ValueTask<bool> ChangeClaimAsync(Uuid tenant, Uuid member, DateOnly week, WorkDigestDispatchStatusView claim,
        Func<WeeklyWorkDigest, bool> change, CancellationToken ct) => ChangeAsync(tenant, member, week, current =>
        {
            var actual = current.ReadDelivery();
            return actual.Status == WorkDigestDispatch.InFlight && actual.Attempts == claim.Attempts &&
                actual.LastAttemptAt == claim.LastAttemptAt && actual.MessageId == claim.MessageId && change(current);
        }, ct);

    async ValueTask<bool> ChangeAsync(Uuid tenantId, Uuid memberId, DateOnly week,
        Func<WeeklyWorkDigest, bool> change, CancellationToken ct)
    {
        try
        {
            var result = await executor.ExecuteAsync(WeeklyWorkDigest.ForDelivery(tenantId, memberId, week), current =>
            {
                var changed = change(current);
                var result = Result<bool>.Success(changed);
                return changed ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);
            }, new RequestContext<WeeklyDeliveryMutation>(new WeeklyDeliveryMutation(tenantId, memberId, week), RequestActor.System), ct).ConfigureAwait(false);
            return result.IsSuccess && result.Value;
        }
        catch (EventStreamConcurrencyException)
        {
            return false;
        }
    }

    static string SkipReason(string? source) => source switch
    {
        "empty_digest" or "empty" => "empty",
        "email_opt_out" or "email_digest_opted_out" or "opted_out" => "opted_out",
        "no_verified_email" or "verified_email_unavailable" => "no_verified_email",
        _ => "inactive_member"
    };

    sealed record WeeklyDeliveryMutation(Uuid TenantId, Uuid MemberId, DateOnly WeekOf) : IRequest;
}
