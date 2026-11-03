using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Services;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Schedules a bounded page of active, opted-in members with verified email addresses.</summary>
public sealed class ScheduleWeeklyWorkDigestHandler(IAggregateExecutor executor,
    ITenantMembershipDirectoryReader memberships, WeeklyWorkDigestRecipientResolver recipients,
    TimeProvider clock, IWeeklyDigestSchedulePages pages, IImmutableContentStore content)
    : IRequestHandler<ScheduleWeeklyWorkDigest, WorkDigestSchedulePageView>
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
        if (request.TenantId == Uuid.Empty || request.WeekStarting.DayOfWeek != DayOfWeek.Monday ||
            request.Limit is < 1 or > MaximumPageSize || !ValidScheduleKey(request.ScheduleKey))
            return Result<WorkDigestSchedulePageView>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "Weekly digest scheduling requires the current Monday-starting week, a bounded page, and a schedule key."));

        var pageId = WeeklyDigestSchedulePage.IdFor(request.TenantId, request.WeekStarting,
            request.ScheduleKey, request.Cursor);
        var admitted = await pages.ReadAsync(request.TenantId, pageId, ct).ConfigureAwait(false);
        if (admitted is not null && admitted.Limit != request.Limit)
            return Result<WorkDigestSchedulePageView>.Failure(FitzKvWeeklyDigestSchedulePages.ChangedIntent());
        if (admitted is null)
        {
            var now = clock.GetUtcNow();
            var currentWeek = WeekOf(now);
            if (request.WeekStarting != currentWeek)
                return Result<WorkDigestSchedulePageView>.Failure(new RequestError(RequestErrorKind.Validation,
                    "A new schedule page requires the current Monday-starting week."));
            var prepared = await PreparePlanAsync(request, pageId, now, ct).ConfigureAwait(false);
            if (!prepared.IsSuccess)
                return Result<WorkDigestSchedulePageView>.Failure(prepared.Error!);
            var planRef = await NativeContent.WriteAsync(content, request.TenantId,
                WeeklyDigestSchedulePage.PlanSchema, prepared.Value, ct).ConfigureAwait(false);
            var accepted = await pages.AcceptIfAbsentAsync(new WeeklyDigestSchedulePage(request.TenantId,
                pageId, request.Limit, planRef, null), ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<WorkDigestSchedulePageView>.Failure(accepted.Error!);
            admitted = accepted.Value;
        }
        var plan = await NativeContent.ReadAsync<WeeklyDigestSchedulePlan>(content, request.TenantId,
            admitted.Plan, ct).ConfigureAwait(false);
        if (plan.TenantId != request.TenantId || plan.PageId != pageId || plan.WeekStarting != request.WeekStarting ||
            plan.AcceptedAt == default || WeekOf(plan.AcceptedAt) != plan.WeekStarting ||
            plan.ScheduleKey != request.ScheduleKey || plan.MemberIds is null || plan.MemberIds.Count > request.Limit ||
            plan.MembersVisited > request.Limit || plan.MembersSkipped < 0 ||
            plan.MembersVisited != plan.MembersSkipped + plan.MemberIds.Count ||
            plan.MemberIds.Any(id => id == Uuid.Empty) || plan.MemberIds.Distinct().Count() != plan.MemberIds.Count)
            throw new ImmutableContentIntegrityException("The schedule page's frozen selection is inconsistent.");
        if (admitted.Result is not null)
            return await OriginalResultAsync(admitted, plan, ct).ConfigureAwait(false);
        var scheduled = 0;
        var actor = ActorReference.ForSystemProcess(WeeklyWorkDigest.SchedulerProcessId,
            "Weekly work digest scheduler");
        var childKey = "schedule-page:" + pageId;
        foreach (var memberId in plan.MemberIds)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var digestId = WeeklyWorkDigest.IdFor(request.TenantId, memberId, request.WeekStarting);
                    var applied = await executor.ExecuteAsync(new WeeklyWorkDigest(request.TenantId, digestId), digest =>
                    {
                        if (digest.Header is { } existing)
                        {
                            if (existing.MemberId != memberId || existing.WeekStarting != request.WeekStarting)
                                throw new ImmutableContentIntegrityException("The scheduled digest has another member or week.");
                            return AggregateOutcome.Discard(Result<int>.Success(existing.ScheduleKey == childKey ? 1 : 0));
                        }
                        var result = digest.Schedule(memberId, request.WeekStarting, childKey, actor, plan.AcceptedAt);
                        return AggregateOutcome.CommitOnSuccess(result.IsSuccess
                            ? Result<int>.Success(1) : Result<int>.Failure(result.Error!));
                    }, context, ct).ConfigureAwait(false);
                    if (!applied.IsSuccess)
                        return Result<WorkDigestSchedulePageView>.Failure(applied.Error!);
                    scheduled += applied.Value;
                    break;
                }
                catch (EventStreamConcurrencyException) when (attempt < 4)
                {
                    // The digest itself tells us which admitted page committed the schedule.
                }
            }
        }
        var response = new WorkDigestSchedulePageView(plan.MembersVisited, scheduled, plan.MembersSkipped,
            plan.NextCursor);
        var frozen = await NativeContent.WriteAsync(content, request.TenantId,
            WeeklyDigestSchedulePage.ResultSchema, response, ct).ConfigureAwait(false);
        var completed = await pages.CompleteAsync(admitted, frozen, ct).ConfigureAwait(false);
        return await OriginalResultAsync(completed, plan, ct).ConfigureAwait(false);
    }

    async Task<Result<WeeklyDigestSchedulePlan>> PreparePlanAsync(ScheduleWeeklyWorkDigest request,
        Uuid pageId, DateTimeOffset now, CancellationToken ct)
    {
        var page = await memberships.ListAsync(request.TenantId, request.Limit, request.Cursor, ct).ConfigureAwait(false);
        if (page.Items.Count > request.Limit)
            return Result<WeeklyDigestSchedulePlan>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The membership directory exceeded the requested page bound."));
        var skipped = 0;
        var selected = new HashSet<Uuid>();
        foreach (var membership in page.Items)
        {
            if (membership.TenantId != request.TenantId || membership.UserId == Uuid.Empty)
            {
                skipped++;
                continue;
            }

            var memberId = RbacIds.Member(request.TenantId, membership.UserId);
            if (selected.Contains(memberId) || await recipients.SuppressionReasonAsync(request.TenantId, memberId, ct)
                    .ConfigureAwait(false) is not null)
            {
                skipped++;
                continue;
            }

            selected.Add(memberId);
        }
        return Result<WeeklyDigestSchedulePlan>.Success(new WeeklyDigestSchedulePlan(request.TenantId, pageId,
            request.WeekStarting, request.ScheduleKey, now, page.Items.Count, skipped, selected.ToArray(), page.NextCursor));
    }

    async Task<Result<WorkDigestSchedulePageView>> OriginalResultAsync(WeeklyDigestSchedulePage page,
        WeeklyDigestSchedulePlan plan, CancellationToken ct)
    {
        var result = await NativeContent.ReadAsync<WorkDigestSchedulePageView>(content, page.TenantId,
            page.Result!, ct).ConfigureAwait(false);
        if (result.MembersVisited != plan.MembersVisited || result.MembersSkipped != plan.MembersSkipped ||
            result.NextCursor != plan.NextCursor || result.DigestsScheduled < 0 ||
            result.DigestsScheduled > plan.MemberIds.Count)
            throw new ImmutableContentIntegrityException("The schedule page's original response differs from its plan.");
        return Result<WorkDigestSchedulePageView>.Success(result);
    }

    static bool ValidScheduleKey(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() && value.Length <= 200 && !value.Any(char.IsControl);

    static DateOnly WeekOf(DateTimeOffset instant)
    {
        var today = DateOnly.FromDateTime(instant.UtcDateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }

    static Result<WorkDigestSchedulePageView> Forbidden() =>
        Result<WorkDigestSchedulePageView>.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the named weekly digest scheduler may schedule member digests."));
}
