using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class CreateClientServiceHandler(ProductClientServiceOperations operations) : IRequestHandler<CreateClientService, ClientServiceRegistration>
{
    public ValueTask<Result<ClientServiceRegistration>> HandleAsync(IRequestContext<CreateClientService> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
