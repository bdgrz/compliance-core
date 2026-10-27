using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Authorizers;

sealed class ScheduleWeeklyWorkDigestAuthorizer : IRequestAuthorizer<ScheduleWeeklyWorkDigest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ScheduleWeeklyWorkDigest> context,
        CancellationToken ct) => ValueTask.FromResult(
        WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.SchedulerProcessId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named weekly digest scheduler may schedule member digests.")));
}
