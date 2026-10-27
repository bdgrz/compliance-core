using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class RecordWorkDigestDeliveryAuthorizer : IRequestAuthorizer<RecordWorkDigestDelivery>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordWorkDigestDelivery> context,
        CancellationToken ct) => ValueTask.FromResult(
        WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the named weekly digest delivery worker may record email outcomes.")));
}
