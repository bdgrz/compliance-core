using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class SeedPlatformOperatorRosterHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<SeedPlatformOperatorRoster>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SeedPlatformOperatorRoster> context, CancellationToken ct) =>
        operations.SeedAsync(context, ct);
}
