using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Correlates an exact canonical tenant member without granting any access.</summary>
public sealed class CorrelatePersonMembershipHandler(PersonOperations operations) : IRequestHandler<CorrelatePersonMembership, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<CorrelatePersonMembership> context, CancellationToken ct) =>
        operations.CorrelateAsync(context, ct);
}
