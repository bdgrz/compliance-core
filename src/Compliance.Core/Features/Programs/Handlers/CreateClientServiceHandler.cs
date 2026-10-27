using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class CreateClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<CreateClientService, ClientServiceRegistration>
{
    public ValueTask<Result<ClientServiceRegistration>> HandleAsync(IRequestContext<CreateClientService> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
