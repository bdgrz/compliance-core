using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class SeedPlatformOperatorRosterHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SeedPlatformOperatorRoster> context, CancellationToken ct) =>
        operations.SeedAsync(context, ct);
}
