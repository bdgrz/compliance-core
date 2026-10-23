using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class TeamMember : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _teamId;
    readonly Uuid _memberId;
    bool _isAssigned;
    Uuid _membershipEpisodeId;
    Uuid _assignmentEventId;
    ulong _assignmentPosition;

    public bool IsAssigned => _isAssigned;
    public Uuid MembershipEpisodeId => _membershipEpisodeId;
    public Uuid AssignmentEventId => _assignmentEventId;
    public ulong AssignmentPosition => _assignmentPosition;

    public TeamMember(Uuid tenantId, Uuid teamId, Uuid memberId)
        : base(
            RbacIds.TeamMember(tenantId, teamId, memberId),
            new EventStreamAddress(
                tenantId.ToString(),
                "rbac-team-members",
                RbacIds.TeamMember(tenantId, teamId, memberId).ToString()))
    {
        _tenantId = tenantId;
        _teamId = teamId;
        _memberId = memberId;
        On<TeamMemberAssigned>(assigned =>
        {
            if (_isAssigned || assigned.TenantId != _tenantId || assigned.TeamId != _teamId ||
                assigned.MemberId != _memberId)
                throw new InvalidOperationException("Assignment requires its exact owner and an empty active slot.");
            _isAssigned = true;
            _membershipEpisodeId = assigned.MembershipEpisodeId;
            _assignmentEventId = assigned.Metadata.EventId;
            _assignmentPosition = assigned.Metadata.AggregateVersion - 1;
        });
        On<TeamMemberRemoved>(removed =>
        {
            if (!_isAssigned || removed.TenantId != _tenantId || removed.TeamId != _teamId ||
                removed.MemberId != _memberId || removed.MembershipEpisodeId != _membershipEpisodeId ||
                removed.AssignmentEventId != _assignmentEventId)
                throw new InvalidOperationException("Removal must close the exact team assignment and membership episode.");
            _isAssigned = false;
            _membershipEpisodeId = Uuid.Empty;
            _assignmentEventId = Uuid.Empty;
        });
    }

    public Result Assign(Uuid membershipEpisodeId = default)
    {
        if (_isAssigned && _membershipEpisodeId != membershipEpisodeId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Settle the prior membership assignment before assigning another episode."));
        if (!_isAssigned || _membershipEpisodeId != membershipEpisodeId)
            RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId, membershipEpisodeId));
        return Result.Success;
    }

    public Result Remove()
    {
        if (!_isAssigned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The member is not on this team."));
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId, _membershipEpisodeId, _assignmentEventId));
        return Result.Success;
    }

    internal Result RemoveForRetirement(MembershipTerminationProof proof, Uuid assignmentEventId)
    {
        if (proof.TenantId != _tenantId || proof.MemberId != _memberId ||
            proof.MembershipEpisodeId == Uuid.Empty || proof.EventId == Uuid.Empty || assignmentEventId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Exact retirement and assignment proofs are required."));
        if (Version == 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The proven team assignment is not available yet.", isTransient: true));
        if (!_isAssigned || _membershipEpisodeId != proof.MembershipEpisodeId || _assignmentEventId != assignmentEventId)
            return Result.Success;
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId, _membershipEpisodeId,
            _assignmentEventId, proof.EventId));
        return Result.Success;
    }
}
