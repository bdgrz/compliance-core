using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
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
