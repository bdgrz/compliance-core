using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class GrantPlatformOperatorHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<GrantPlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct) =>
        operations.GrantAsync(context, ct);
}
