using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class ScheduleWeeklyWorkDigestAuthorizer : IRequestAuthorizer<ScheduleWeeklyWorkDigest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<ScheduleWeeklyWorkDigest> context,
        CancellationToken ct) => ValueTask.FromResult(
        WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.SchedulerProcessId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named weekly digest scheduler may schedule member digests.")));
}
