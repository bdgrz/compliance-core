using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
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
    ulong _assignmentGeneration;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsAssigned => _isAssigned;
    public Uuid MembershipEpisodeId => _membershipEpisodeId;
    public Uuid AssignmentEventId => _assignmentEventId;
    public ulong AssignmentPosition => _assignmentPosition;

    /// <summary>Logical revision of this assignment slot: the number of facts accepted on the owner so far.</summary>
    public ulong AssignmentRevision => Version;

    /// <summary>Number of assignments ever made on this owner; the current assignment's generation while assigned.</summary>
    public ulong AssignmentGeneration => _assignmentGeneration;

    /// <summary>The member is already on the team for this episode; dispatchers that assign as a side effect continue past it.</summary>
    public static RequestError AlreadyAssignedConflict { get; } = new(RequestErrorKind.Conflict,
        "The member is already on this team for this membership episode.", false);

    public TeamMember(Uuid tenantId, Uuid teamId, Uuid memberId, Uuid selectedCommandId = default)
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
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<TeamMemberAssigned>(assigned =>
        {
            if (_isAssigned || assigned.TenantId != _tenantId || assigned.TeamId != _teamId ||
                assigned.MemberId != _memberId || assigned.MembershipEpisodeId == Uuid.Empty)
                throw new InvalidOperationException("Assignment requires its exact owner and an empty active slot.");
            RequireOwnOperation(assigned.CommandId, assigned.Operation);
            Acceptance.Observe(assigned);
            _isAssigned = true;
            _assignmentGeneration++;
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
            RequireOwnOperation(removed.CommandId, removed.Operation);
            Acceptance.Observe(removed);
            _isAssigned = false;
            _membershipEpisodeId = Uuid.Empty;
            _assignmentEventId = Uuid.Empty;
        });
    }

    /// <summary>
    ///     Checks the assignment guards against hydrated state without raising anything; the first half of the
    ///     native acceptance pair. Negative expectations are unconstrained and reserved for system callers.
    ///     <paramref name="removalPending" /> is true when the guard admitted a removal of this assignment that is not
    ///     applied yet, which the assignment then waits behind.
    /// </summary>
    public Result PrepareAssign(Uuid membershipEpisodeId, long expectedRevision, long expectedGeneration,
        bool removalPending = false)
    {
        if (membershipEpisodeId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team assignment requires its membership episode."));
        var expected = CheckExpected(expectedRevision, expectedGeneration);
        if (!expected.IsSuccess)
            return expected;
        if (!_isAssigned)
            return Result.Success;
        return _membershipEpisodeId == membershipEpisodeId
            ? removalPending ? Result.Success : Result.Failure(AlreadyAssignedConflict)
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Settle the prior membership assignment before assigning another episode."));
    }

    /// <summary>Accepts the assignment as the native fact of <paramref name="operation" />.</summary>
    public Result Assign(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid membershipEpisodeId,
        long expectedRevision, long expectedGeneration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team assignment requires its own logical operation."));
        var prepared = PrepareAssign(membershipEpisodeId, expectedRevision, expectedGeneration);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId, membershipEpisodeId, operation.LogicalId,
            operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks the removal guards against hydrated state without raising anything. An empty episode and negative
    ///     expectations are unconstrained and reserved for system callers.
    /// </summary>
    public Result PrepareRemove(Uuid membershipEpisodeId, long expectedRevision, long expectedGeneration)
    {
        var expected = CheckExpected(expectedRevision, expectedGeneration);
        if (!expected.IsSuccess)
            return expected;
        if (!_isAssigned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The member is not on this team."));
        return membershipEpisodeId != Uuid.Empty && membershipEpisodeId != _membershipEpisodeId
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The team assignment belongs to a different membership episode."))
            : Result.Success;
    }

    /// <summary>Accepts the removal as the native fact of <paramref name="operation" />.</summary>
    public Result Remove(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid membershipEpisodeId,
        long expectedRevision, long expectedGeneration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team removal requires its own logical operation."));
        var prepared = PrepareRemove(membershipEpisodeId, expectedRevision, expectedGeneration);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId, _membershipEpisodeId, _assignmentEventId,
            null, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Accepts an assignment a process receiver applies on behalf of an admitted root operation. The fact carries
    ///     no first response: the guard fact that admitted the operation owns it.
    /// </summary>
    public Result ApplyAssignment(OperationProvenance operation, Uuid membershipEpisodeId, long expectedRevision,
        long expectedGeneration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team assignment requires its own logical operation."));
        var prepared = PrepareAssign(membershipEpisodeId, expectedRevision, expectedGeneration);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamMemberAssigned(_tenantId, _teamId, _memberId, membershipEpisodeId, operation.LogicalId,
            operation, null));
        return Result.Success;
    }

    /// <summary>Accepts a removal a process receiver applies on behalf of an admitted root operation.</summary>
    public Result ApplyRemoval(OperationProvenance operation, Uuid membershipEpisodeId, long expectedRevision,
        long expectedGeneration)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A team removal requires its own logical operation."));
        var prepared = PrepareRemove(membershipEpisodeId, expectedRevision, expectedGeneration);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId, _membershipEpisodeId, _assignmentEventId,
            null, operation.LogicalId, operation, null));
        return Result.Success;
    }

    /// <summary>The provenance of an operation on this owner; the owner is its own first, affected and revision scope.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, ActorReference actor, string episode,
        ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt, Uuid? causationEventId = null) =>
        new(commandId, commandId, causationEventId, commandKind, actor, Stream, episode, originalExpectedRevision,
            authoredIntentSha256, acceptedAt, Stream, Stream);

    /// <summary>The provenance of a process receiver's operation, which keeps the root operation it serves.</summary>
    public OperationProvenance Provenance(Uuid logicalId, Uuid rootOperationId, Uuid? causationEventId,
        string commandKind, ActorReference actor, string episode, ulong? originalExpectedRevision,
        string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(logicalId, rootOperationId, causationEventId, commandKind, actor, Stream, episode,
            originalExpectedRevision, authoredIntentSha256, acceptedAt, Stream, Stream);

    internal Result RemoveForRetirement(MembershipTerminationProof proof, Uuid assignmentEventId, Uuid relationshipId,
        DateTimeOffset acceptedAt)
    {
        if (proof.TenantId != _tenantId || proof.MemberId != _memberId ||
            proof.MembershipEpisodeId == Uuid.Empty || proof.EventId == Uuid.Empty || assignmentEventId == Uuid.Empty ||
            relationshipId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Exact retirement and assignment proofs are required."));
        if (Version == 0)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The proven team assignment is not available yet.", isTransient: true));
        if (!_isAssigned || _membershipEpisodeId != proof.MembershipEpisodeId || _assignmentEventId != assignmentEventId)
            return Result.Success;
        var operation = Provenance(relationshipId, "CleanupRetiredMembershipRelationship",
            ActorReference.ForSystemProcess("system", "system"), proof.MembershipEpisodeId.ToString(), null,
            ContentDigest.Sha256(System.Text.Encoding.UTF8.GetBytes(
                $"{relationshipId}:{assignmentEventId}:{proof.EventId}")), acceptedAt, proof.EventId);
        RaiseEvent(new TeamMemberRemoved(_tenantId, _teamId, _memberId, _membershipEpisodeId,
            _assignmentEventId, proof.EventId, relationshipId, operation, null));
        return Result.Success;
    }

    Result CheckExpected(long expectedRevision, long expectedGeneration)
    {
        if (expectedRevision >= 0 && (ulong)expectedRevision != AssignmentRevision ||
            expectedGeneration >= 0 && (ulong)expectedGeneration != _assignmentGeneration)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The team assignment changed since it was read."));
        return Result.Success;
    }

    void RequireOwnOperation(Uuid commandId, OperationProvenance operation)
    {
        if (commandId == Uuid.Empty || operation.LogicalId != commandId || operation.Owner != Stream)
            throw new InvalidOperationException(
                "A team assignment fact must accept its own logical operation on this owner.");
    }
}
