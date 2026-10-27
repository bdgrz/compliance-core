using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Commands;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class CreateClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<CreateClientService, ClientServiceRegistration>
{
    public ValueTask<Result<ClientServiceRegistration>> HandleAsync(IRequestContext<CreateClientService> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
