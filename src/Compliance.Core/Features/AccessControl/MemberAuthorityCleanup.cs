using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Recovers exact retired-episode authority from native source facts, independently of directories.</summary>
public sealed class MemberAuthorityCleanup(IAggregateReader reader, IAggregateExecutor executor,
    IDomainEventReader events, IProjectionCheckpointStore checkpoints, TimeProvider clock)
{
    const int MaximumAttempts = 5;

    internal async ValueTask<Result> ReconcileTeamAssignmentAsync(IExecutionContext parent,
        AssignTeamMember assignment, Uuid episodeId, Uuid sourceEventId, ulong position, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(assignment.TenantId, assignment.MemberId,
            episodeId), ct).ConfigureAwait(false);
        if (member.TerminationProof is not { } proof)
            return DependencyUnavailable();
        var request = new CleanupRetiredMembershipRelationship(assignment.TenantId, assignment.MemberId, episodeId,
            RetiredMembershipRelationshipKind.TeamAssignment, assignment.TeamId, sourceEventId, position, proof.EventId,
            MemberRetirementEffects.RelationshipId(sourceEventId,
                new TeamMember(assignment.TenantId, assignment.TeamId, assignment.MemberId).Stream,
                episodeId, position, proof.EventId));
        return await ReconcileAsync(Context(request, parent), ct).ConfigureAwait(false);
    }

    internal ValueTask<Result> CompleteAsync(IExecutionContext parent, MembershipTerminationProof proof,
        CancellationToken ct)
    {
        var request = MemberRetirementEffects.Complete(proof);
        return CompleteAsync(Context(request, parent), ct);
    }

    internal async ValueTask<Result> CompleteAsync(IRequestContext<CompleteMemberRetirement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty ||
            request.MembershipEpisodeId == Uuid.Empty || request.TerminationEventId == Uuid.Empty ||
            request.CommandId != MemberRetirementEffects.CompletionId(request.TerminationEventId,
                request.MemberId, request.MembershipEpisodeId))
            return InvalidProof();
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, request.MemberId,
            request.MembershipEpisodeId), ct).ConfigureAwait(false);
        if (member.TerminationProof is not { } proof || proof.EventId != request.TerminationEventId)
            return InvalidProof();
        if (member.SelectedCleanupCompleted)
            return Result.Success;

        foreach (var area in new[] { "rbac-team-members", "access-grants" })
        {
            var pattern = EventStreamPattern.ForPattern(request.TenantId.ToString(), area);
            var cut = ProjectionCheckpoint.Start;
            var candidates = new List<CleanupRetiredMembershipRelationship>();
            await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct)
                               .WithCancellation(ct).ConfigureAwait(false))
            {
                cut = new ProjectionCheckpoint(record.NextCursor);
                var candidate = record.Event switch
                {
                    TeamMemberAssigned assigned when assigned.TenantId == proof.TenantId &&
                        assigned.MemberId == proof.MemberId && assigned.MembershipEpisodeId == proof.MembershipEpisodeId =>
                        MemberRetirementEffects.Team(assigned, record.ResourceOffset, proof.EventId),
                    AccessGrantIssued issued when issued.TenantId == proof.TenantId &&
                        issued.Terms.Principal.Kind == AccessGrantPrincipalKind.Member &&
                        issued.Terms.Principal.Id == proof.MemberId && issued.MembershipEpisodeId == proof.MembershipEpisodeId =>
                        MemberRetirementEffects.Grant(issued, record.ResourceOffset, proof.EventId),
                    _ => null,
                };
                if (candidate is null)
                    continue;
                if (record.Stream != MemberRetirementEffects.RelationshipStream(candidate) || record.Event.Metadata.IsAudit)
                    return InvalidProof();
                candidates.Add(candidate);
            }
            foreach (var candidate in candidates.OrderBy(static candidate =>
                         MemberRetirementEffects.RelationshipStream(candidate).ToString(), StringComparer.Ordinal)
                         .ThenBy(static candidate => candidate.SourcePosition))
            {
                var result = await ReconcileAsync(Context(candidate, context), ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return result;
            }
            // Source discovery is technical progress, outside domain events. Always rescan from
            // the source on recovery; a saved cut never permits skipping unfinished receivers.
            await checkpoints.SaveAsync(new CheckpointIdentity($"MemberRetirementCoverage:{request.CommandId}",
                pattern), cut, ct).ConfigureAwait(false);
        }
        return await RetryAsync(() => executor.ExecuteAsync(new Member(proof.TenantId, proof.UserId,
                proof.MembershipEpisodeId), current => AggregateOutcome.CommitOnSuccess(
                current.CompleteDeprovisionCleanup(proof, request.CommandId, clock.GetUtcNow())), context, ct), ct)
            .ConfigureAwait(false);
    }

    internal async ValueTask<Result> ReconcileAsync(IRequestContext<CleanupRetiredMembershipRelationship> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Forbidden();
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty || request.TargetId == Uuid.Empty ||
            request.MembershipEpisodeId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            request.TerminationEventId == Uuid.Empty || !Enum.IsDefined(request.Kind))
            return InvalidProof();
        var owner = request.Kind == RetiredMembershipRelationshipKind.TeamAssignment
            ? RbacIds.TeamMember(request.TenantId, request.TargetId, request.MemberId) : request.TargetId;
        if (request.CommandId != MemberRetirementEffects.RelationshipId(request.SourceEventId,
                MemberRetirementEffects.RelationshipStream(request), request.MembershipEpisodeId, request.SourcePosition,
                request.TerminationEventId))
            return InvalidProof();
        var stream = MemberRetirementEffects.RelationshipStream(request);
        DomainEventRecord? source = null;
        await foreach (var record in events.ReadAsync(stream, request.SourcePosition, ct)
                           .WithCancellation(ct).ConfigureAwait(false))
        {
            source = record;
            break;
        }
        if (source is null)
            return DependencyUnavailable();
        if (source.Stream != stream || source.ResourceOffset != request.SourcePosition ||
            source.Event.Metadata.EventId != request.SourceEventId || source.Event.Metadata.AggregateId != owner ||
            source.Event.Metadata.IsAudit || source.Event.Metadata.AggregateVersion != request.SourcePosition + 1)
            return InvalidProof();
        var exact = source.Event switch
        {
            TeamMemberAssigned assigned when request.Kind == RetiredMembershipRelationshipKind.TeamAssignment =>
                MemberRetirementEffects.Team(assigned, source.ResourceOffset, request.TerminationEventId) == request,
            AccessGrantIssued issued when request.Kind == RetiredMembershipRelationshipKind.DirectGrant &&
                issued.Terms.Principal.Kind == AccessGrantPrincipalKind.Member =>
                MemberRetirementEffects.Grant(issued, source.ResourceOffset, request.TerminationEventId) == request,
            _ => false,
        };
        if (!exact)
            return InvalidProof();
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, request.MemberId,
            request.MembershipEpisodeId), ct).ConfigureAwait(false);
        if (member.TerminationProof is not { } proof)
            return DependencyUnavailable();
        if (proof.EventId != request.TerminationEventId)
            return InvalidProof();
        return request.Kind == RetiredMembershipRelationshipKind.TeamAssignment
            ? await RetryAsync(() => executor.ExecuteAsync(new TeamMember(request.TenantId, request.TargetId,
                    request.MemberId), current => AggregateOutcome.CommitOnSuccess(
                    current.RemoveForRetirement(proof, request.SourceEventId,
                        request.CommandId, clock.GetUtcNow())), context, ct), ct).ConfigureAwait(false)
            : await RetryAsync(() => executor.ExecuteAsync(new AccessGrant(request.TenantId, request.TargetId),
                    current => AggregateOutcome.CommitOnSuccess(current.RevokeForRetirement(proof,
                        request.SourceEventId, clock.GetUtcNow())), context, ct), ct).ConfigureAwait(false);
    }

    static RequestContext<T> Context<T>(T request, IExecutionContext parent) where T : IRequestBase =>
        new(request, new RequestDispatchContext(RequestActor.System,
            metadata: RequestMetadata.FromRequest(request, parent)));

    static async ValueTask<Result> RetryAsync(Func<ValueTask<Result>> action, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt + 1 < MaximumAttempts)
            {
                // Rehydrate the exact receiving scope; never retarget a newer assignment.
            }
        }
        return DependencyUnavailable();
    }

    static Result InvalidProof() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Membership cleanup requires the exact authoritative source fact and episode."));
    static Result DependencyUnavailable() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "Membership cleanup source authority is not available yet.", isTransient: true));
    static Result Forbidden() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only system recovery may execute membership cleanup."));
}
