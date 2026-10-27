using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Schedules a bounded page of active, opted-in members with verified email addresses.</summary>
public sealed class ScheduleWeeklyWorkDigestHandler(IAggregateExecutor executor,
    ITenantMembershipDirectoryReader memberships, WeeklyWorkDigestRecipientResolver recipients,
    TimeProvider clock) : IRequestHandler<ScheduleWeeklyWorkDigest, WorkDigestSchedulePageView>
{
    const int MaximumPageSize = 200;

    public ValueTask<Result<WorkDigestSchedulePageView>> HandleAsync(
        IRequestContext<ScheduleWeeklyWorkDigest> context, CancellationToken ct) =>
        WorkDigestSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<WorkDigestSchedulePageView>> HandleCoreAsync(
        IRequestContext<ScheduleWeeklyWorkDigest> context, CancellationToken ct)
    {
        if (!WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.SchedulerProcessId))
            return Forbidden();

        var request = context.Request;
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var currentWeekStarting = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (request.TenantId == Uuid.Empty || request.WeekStarting != currentWeekStarting ||
            request.Limit is < 1 or > MaximumPageSize || !ValidScheduleKey(request.ScheduleKey))
            return Result<WorkDigestSchedulePageView>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Weekly digest scheduling requires the current Monday-starting week, a bounded page, and a schedule key."));

        var page = await memberships.ListAsync(request.TenantId, request.Limit, request.Cursor, ct)
            .ConfigureAwait(false);
        var scheduled = 0;
        var skipped = 0;
        foreach (var membership in page.Items)
        {
            if (membership.TenantId != request.TenantId || membership.UserId == Uuid.Empty)
            {
                skipped++;
                continue;
            }

            var memberId = RbacIds.Member(request.TenantId, membership.UserId);
            if (await recipients.SuppressionReasonAsync(request.TenantId, memberId, ct)
                    .ConfigureAwait(false) is not null)
            {
                skipped++;
                continue;
            }

            var digestId = WeeklyWorkDigest.IdFor(request.TenantId, memberId, request.WeekStarting);
            var actor = ActorReference.ForSystemProcess(WeeklyWorkDigest.SchedulerProcessId,
                "Weekly work digest scheduler");
            var result = await executor.ExecuteAsync(new WeeklyWorkDigest(request.TenantId, digestId),
                digest =>
                {
                    var isNew = digest.Revision == 0;
                    var scheduled = digest.Schedule(memberId, request.WeekStarting,
                        request.ScheduleKey, actor, now);
                    return AggregateOutcome.CommitOnSuccess(scheduled.IsSuccess
                        ? Result<int>.Success(isNew ? 1 : 0)
                        : Result<int>.Failure(scheduled.Error));
                }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result<WorkDigestSchedulePageView>.Failure(result.Error);
            scheduled += result.Value;
        }

        return Result<WorkDigestSchedulePageView>.Success(new WorkDigestSchedulePageView(
            page.Items.Count, scheduled, skipped, page.NextCursor));
    }

    static bool ValidScheduleKey(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() && value.Length <= 200 && !value.Any(char.IsControl);

    static Result<WorkDigestSchedulePageView> Forbidden() =>
        Result<WorkDigestSchedulePageView>.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the named weekly digest scheduler may schedule member digests."));
}
