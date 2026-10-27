using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class RevokePlatformOperatorHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<RevokePlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct) =>
        operations.RevokeAsync(context, ct);
}
