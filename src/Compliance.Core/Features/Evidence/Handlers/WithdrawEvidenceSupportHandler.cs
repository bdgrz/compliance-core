using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

/// <summary>Admits a current client lead's reasoned withdrawal before any accepted retry is returned.</summary>
public sealed class WithdrawEvidenceSupportHandler(IAggregateExecutor executor, IAggregateReader reader,
    IPermissionAuthorizer permissions, TimeProvider clock) : IRequestHandler<WithdrawEvidenceSupport>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<WithdrawEvidenceSupport> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a current client lead may withdraw evidence support."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.SupportId == Uuid.Empty || request.ExpectedRevision < 0 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Withdrawal requires its exact scope, expected revision, and a bounded reason."));
        var now = clock.GetUtcNow();
        var memberId = RbacIds.Member(request.TenantId, userId);
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, memberId), ct).ConfigureAwait(false);
        if (!member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) ||
            member.UserId != userId || member.Affiliation == "firm_staff" ||
            !await permissions.IsAllowedAsync(request.TenantId, userId, memberId,
                EvidenceSupportPermissions.Withdraw, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a current client lead may withdraw evidence support."));

        var support = await reader.HydrateAsync(EvidenceSupport.ForVerification(request.TenantId, request.SupportId), ct)
            .ConfigureAwait(false);
        if (support.Current?.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence support link was not found in this program."));
        var actor = ActorReference.ForMember(memberId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await executor.ExecuteAsync(new EvidenceSupport(request.TenantId, request.ProgramId, request.SupportId),
            owner => CommandFailureRequestAdapter.ToOutcome(owner.Withdraw(request, actor, now)), context, ct)
            .ConfigureAwait(false);
    }
}
