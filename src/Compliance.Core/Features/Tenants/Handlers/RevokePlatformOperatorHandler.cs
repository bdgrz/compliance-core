using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Services;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class RevokePlatformOperatorHandler(PlatformOperatorOperations operations) : IRequestHandler<RevokePlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct) =>
        operations.RevokeAsync(context, ct);
}
