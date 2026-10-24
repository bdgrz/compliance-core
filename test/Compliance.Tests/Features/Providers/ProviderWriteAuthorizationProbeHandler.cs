using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

sealed class ProviderWriteAuthorizationProbeHandler : IRequestHandler<RecordProvider, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<RecordProvider> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<OperationAcceptedResponse>.Success(
            new OperationAcceptedResponse(context.RequestId, "/operations", DateTimeOffset.UnixEpoch)));
}
