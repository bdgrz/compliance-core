using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Assigns a team member. Reassignment to the built-in Administrators team restores the
///     member in the tenant manager guard first (#432).
/// </summary>
public sealed class AssignTeamMemberHandler(IAggregateExecutor executor,
    TenantManagerInvariant managers, IAggregateReader reader, MemberAuthorityCleanup cleanup) : IRequestHandler<AssignTeamMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        if (!member.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));

        var prior = await reader.HydrateAsync(new TeamMember(request.TenantId, request.TeamId,
            request.MemberId), ct).ConfigureAwait(false);
        if (prior.IsAssigned && prior.MembershipEpisodeId != member.MembershipEpisodeId)
        {
            var settled = await cleanup.ReconcileTeamAssignmentAsync(context, request, prior.MembershipEpisodeId,
                prior.AssignmentEventId, prior.AssignmentPosition, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return settled;
        }

        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId))
        {
            foreach (var reason in new[]
                     {
                         TenantManagerInvariant.Suspended,
                         TenantManagerInvariant.Deprovisioned,
                         TenantManagerInvariant.RemovedFromAdministrators,
                     })
            {
                var restored = await managers.RestoreAsync(context, request.TenantId,
                    request.MemberId, reason, ct).ConfigureAwait(false);
                if (!restored.IsSuccess)
                    return restored;
            }
        }
        var membershipEpisodeId = member.MembershipEpisodeId;
        var assignmentEventId = Uuid.Empty;
        ulong assignmentPosition = 0;
        var result = await executor.ExecuteAsync(
            new TeamMember(request.TenantId, request.TeamId, request.MemberId),
            assignment =>
            {
                var assigned = assignment.Assign(membershipEpisodeId);
                assignmentEventId = assignment.AssignmentEventId;
                assignmentPosition = assignment.AssignmentPosition;
                return AggregateOutcome.CommitOnSuccess(assigned);
            },
            context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;

        var currentMember = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        if (currentMember.IsRegistered && currentMember.MembershipEpisodeId == membershipEpisodeId)
            return Result.Success;

        var compensated = await cleanup.ReconcileTeamAssignmentAsync(context, request, membershipEpisodeId,
            assignmentEventId, assignmentPosition, ct).ConfigureAwait(false);
        if (!compensated.IsSuccess)
            return compensated;
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The tenant member changed before the team assignment completed."));
    }

}
