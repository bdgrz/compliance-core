using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Readers;

public sealed partial class WorkQueueReader
{
    internal async ValueTask<Result> AuthorizeAssignmentActorAsync(Uuid tenantId, OperationsActor actor, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, actor.MemberId), ct).ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && !member.IsDeprovisioned && member.Affiliation != "firm_staff" &&
            member.UserId == actor.UserId && RbacIds.Member(tenantId, actor.UserId) == actor.MemberId
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current active canonical client membership is required."));
    }

    internal async ValueTask<Result> AuthorizeAssignmentReceiptAsync(Uuid tenantId, Uuid programId, OperationsActor actor,
        WorkCandidate source, string commandKind, CancellationToken ct)
    {
        var actorAuthorization = await AuthorizeAssignmentActorAsync(tenantId, actor, ct).ConfigureAwait(false);
        if (!actorAuthorization.IsSuccess)
            return actorAuthorization;
        if (!await CanReadSourceAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false) ||
            !await CanAssignAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current source visibility and management independence are required for the original receipt."));
        if ((RiskAcceptanceWork.HasRestrictedAuthority(source) || source.Kind == FindingClosureWork.Kind) &&
            !await IsEligibleAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current restricted source decision authority is required for this original receipt."));
        if (commandKind == nameof(AssignWorkItem))
            return await authority.ManagesProgramAsync(tenantId, actor, programId, ct).ConfigureAwait(false)
                ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current program management authority is required for the original assignment receipt."));
        if (commandKind == nameof(ClaimWorkItem) && (source.Responsible.Kind != OperatingAuthority.TeamHolder ||
                !await HoldsAsync(tenantId, source.Responsible, actor.MemberId, ct).ConfigureAwait(false)))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current membership of the source holding team is required for this claim receipt."));
        return await IsEligibleAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false)
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current source operating authority is required for this original receipt."));
    }
}
