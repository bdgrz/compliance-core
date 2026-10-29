using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Commands;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class RetireClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<RetireClientService>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RetireClientService> context, CancellationToken ct) =>
        operations.RetireAsync(context, ct);
}
