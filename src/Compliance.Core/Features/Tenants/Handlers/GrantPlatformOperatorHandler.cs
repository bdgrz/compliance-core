using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class GrantPlatformOperatorHandler(PlatformOperatorOperations operations) : IRequestHandler<GrantPlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct) =>
        operations.GrantAsync(context, ct);
}
