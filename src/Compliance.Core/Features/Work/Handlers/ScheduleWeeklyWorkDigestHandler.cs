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
namespace Bdgrz.Compliance.Features.Work.Handlers;

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
