using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class RevokePlatformOperatorHandler(PlatformOperatorRosterOperations operations) : IRequestHandler<RevokePlatformOperator>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct) =>
        operations.RevokeAsync(context, ct);
}
