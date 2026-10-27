using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ReviseClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<ReviseClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseClientService> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
