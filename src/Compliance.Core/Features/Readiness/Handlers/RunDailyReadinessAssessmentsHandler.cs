using System.Security.Claims;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Portia owns scheduled delivery and retries; a program/day owns the repeat no-op.</summary>
public sealed class RunDailyReadinessAssessmentsHandler(IProgramDirectoryReader directory,
    ProgramApplicationAdmission programs, IRequestBus bus, TimeProvider clock)
    : IRequestHandler<RunDailyReadinessAssessments>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RunDailyReadinessAssessments> context,
        CancellationToken ct)
    {
        var subjects = context.Actor.FindAll(ClaimTypes.NameIdentifier).ToArray();
        if (!RequestActor.IsSystem(context.Actor) || context.Invocation is not (ScheduleInvocation or DirectInvocation) ||
            subjects.Length != 1 || subjects[0].Issuer != "bdgrz.system" || subjects[0].Value != DailyReadinessAssessment.ProcessId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only the named daily scheduler may run readiness."));
        var now = clock.GetUtcNow();
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var tenant = context.Request.TenantId;
        string? cursor = null;
        var seen = new HashSet<Uuid>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await directory.ListAsync(tenant, 200, cursor, ct).ConfigureAwait(false);
            foreach (var candidate in page.Items)
            {
                if (candidate.TenantId != tenant || candidate.ProgramId == Uuid.Empty)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The program roster contains another tenant or empty identity."));
                if (!seen.Add(candidate.ProgramId))
                    continue;
                var active = await programs.CheckAsync(tenant, candidate.ProgramId, ct).ConfigureAwait(false);
                if (!active.IsSuccess)
                {
                    if (active.Error.Kind is RequestErrorKind.NotFound or RequestErrorKind.Forbidden)
                        continue;
                    return active;
                }
                var request = new RecordReadinessAssessment(DailyReadinessAssessment.IdFor(tenant, candidate.ProgramId, day),
                    tenant, candidate.ProgramId, null, day);
                var dispatch = bus.CreateContext(RequestActor.CreateSystem(DailyReadinessAssessment.ProcessId, "bdgrz.system"));
                var result = await bus.DispatchAsync(request, dispatch, ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return Result.Failure(result.Error);
            }
            cursor = page.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The program roster repeated a page cursor."));
        } while (cursor is not null);
        return Result.Success;
    }
}
