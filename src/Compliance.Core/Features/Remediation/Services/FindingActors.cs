using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
namespace Bdgrz.Compliance.Features.Remediation.Services;

/// <summary>Checks current source membership before native accepted-operation replay can return a receipt.</summary>
static class FindingActors
{
    public static async ValueTask<Result<OperationsActor>> RequireCurrentAsync(ClaimsPrincipal principal,
        Uuid tenantId, IAggregateReader reader, DateTimeOffset now, CancellationToken ct)
    {
        if (RequestActor.IsSystem(principal) || !UserIdentityClaims.TryGetBdgrzSubject(principal, out _))
            return Failure();
        var actor = OperationsActor.From(principal, tenantId);
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, actor.MemberId), ct).ConfigureAwait(false);
        return member.UserId == actor.UserId && member.Affiliation != "firm_staff" &&
            member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime))
            ? Result<OperationsActor>.Success(actor) : Failure();
    }

    static Result<OperationsActor> Failure() => Result<OperationsActor>.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Finding changes require a current active client member."));
}
