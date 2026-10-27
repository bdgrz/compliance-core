using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

sealed class ProviderWriteAuthorizationProbeHandler : IRequestHandler<RecordProvider, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<RecordProvider> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<OperationAcceptedResponse>.Success(
            new OperationAcceptedResponse(context.RequestId, "/operations", DateTimeOffset.UnixEpoch)));
}
