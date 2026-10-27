using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Correlates an exact canonical tenant member without granting any access.</summary>
public sealed class CorrelatePersonMembershipHandler(PersonOperations operations) : IRequestHandler<CorrelatePersonMembership, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<CorrelatePersonMembership> context, CancellationToken ct) =>
        operations.CorrelateAsync(context, ct);
}
