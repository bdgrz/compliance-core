using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class DeliverWeeklyWorkDigestAuthorizer : IRequestAuthorizer<DeliverWeeklyWorkDigest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<DeliverWeeklyWorkDigest> context,
        CancellationToken ct) => ValueTask.FromResult(
        WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named weekly digest delivery worker may request an email attempt.")));
}
