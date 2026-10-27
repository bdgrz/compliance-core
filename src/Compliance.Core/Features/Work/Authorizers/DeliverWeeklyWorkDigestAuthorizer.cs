using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Cntryl.Portia;

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
