using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Authorizers;

sealed class DeliverWeeklyWorkDigestAuthorizer : IRequestAuthorizer<DeliverWeeklyWorkDigest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<DeliverWeeklyWorkDigest> context,
        CancellationToken ct) => ValueTask.FromResult(
        WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named weekly digest delivery worker may request an email attempt.")));
}
