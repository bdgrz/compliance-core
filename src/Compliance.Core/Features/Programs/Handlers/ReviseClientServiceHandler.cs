using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ReviseClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<ReviseClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseClientService> context, CancellationToken ct) =>
        operations.ReviseAsync(context, ct);
}
