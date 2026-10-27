using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class RetireClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<RetireClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireClientService> context, CancellationToken ct) =>
        operations.RetireAsync(context, ct);
}
