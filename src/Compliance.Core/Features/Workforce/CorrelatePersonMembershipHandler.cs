using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Correlates an exact canonical tenant member without granting any access.</summary>
public sealed class CorrelatePersonMembershipHandler(PersonOperations operations) : IRequestHandler<CorrelatePersonMembership, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<CorrelatePersonMembership> context, CancellationToken ct) =>
        operations.CorrelateAsync(context, ct);
}
