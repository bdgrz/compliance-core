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
public sealed class CorrelatePersonMembershipHandler : IRequestHandler<CorrelatePersonMembership, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<CorrelatePersonMembership> context, CancellationToken ct) =>
        ValueTask.FromResult(Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
            "Use LinkPersonToMember or UnlinkPersonFromMember; singular correlation is retired.")));
}
