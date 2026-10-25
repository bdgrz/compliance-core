using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class RetireClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<RetireClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireClientService> context, CancellationToken ct) =>
        operations.RetireAsync(context, ct);
}
