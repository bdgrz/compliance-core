using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class GrantPlatformOperatorHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<GrantPlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct) =>
        operations.GrantAsync(context, ct);
}
