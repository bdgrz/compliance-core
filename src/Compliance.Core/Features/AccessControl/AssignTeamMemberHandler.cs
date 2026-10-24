using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Assigns a team member. A non-administrator team is a direct native operation on the team-member owner and
///     never reads the tenant manager guard; reassignment to the built-in Administrators team still restores the
///     member in the guard first (#432) until the guard-first owner replaces that branch.
/// </summary>
public sealed class AssignTeamMemberHandler(IAggregateExecutor executor, NativeOperationExecutor operations,
    TenantManagerInvariant managers, IAggregateReader reader, MemberAuthorityCleanup cleanup,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AssignTeamMember, AdministratorTransitionResponse>
{
    const string CommandKind = "AssignTeamMember";
    const string CurrentEpisode = "current";

    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<AssignTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var isSystem = RequestActor.IsSystem(context.Actor);
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.TeamId == Uuid.Empty ||
            request.MemberId == Uuid.Empty || (request.MemberEpisodeId == Uuid.Empty && !isSystem))
            return Fail(RequestErrorKind.Validation, "A command id, tenant, team, member and member episode are required.");
        if ((request.ExpectedAssignmentRevision < 0 || request.ExpectedAssignmentGeneration < 0) && !isSystem)
            return Fail(RequestErrorKind.Validation, "The expected assignment revision and generation must not be negative.");
        var actor = RegisterMemberHandler.ActorOf(context.Actor);
        if (actor is null)
            return Fail(RequestErrorKind.Forbidden, "The caller identity is not recognized.");

        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, request.MemberId), ct)
            .ConfigureAwait(false);
        var provenance = Provenance(request, actor, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var prior = await reader.HydrateAsync(Owner(request, request.CommandId), ct).ConfigureAwait(false);
        var decision = prior.Acceptance.Decide(binding);
        var replay = decision != NativeAcceptanceDecision.None;
        var administrators = request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId);
        var episodeId = request.MemberEpisodeId == Uuid.Empty ? member.MembershipEpisodeId : request.MemberEpisodeId;

        if (administrators && replay)
            return decision == NativeAcceptanceDecision.Conflict
                ? Result<AdministratorTransitionResponse>.Failure(AcceptedAppendResult.ConflictError)
                : Result<AdministratorTransitionResponse>.Success(new AdministratorTransitionResponse(
                    request.CommandId, AdministratorTransitionResponseState.Completed, null,
                    prior.Acceptance.SelectedOperation!.Operation.AcceptedAt));
        if (!replay && administrators && !member.IsRegistered)
            return Fail(RequestErrorKind.NotFound, "The tenant member was not found.");
        if (!replay && prior.IsAssigned && prior.MembershipEpisodeId != member.MembershipEpisodeId)
        {
            var settled = await cleanup.ReconcileTeamAssignmentAsync(context, request, prior.MembershipEpisodeId,
                prior.AssignmentEventId, prior.AssignmentPosition, ct).ConfigureAwait(false);
            if (!settled.IsSuccess)
                return Result<AdministratorTransitionResponse>.Failure(settled.Error!);
        }

        if (administrators)
            return await AssignAdministratorAsync(context, request, member, provenance, ct).ConfigureAwait(false);

        var accepted = await operations.ExecuteAsync(new NativeOperation<TeamMember>(binding,
            () => Owner(request, request.CommandId), owner => owner.Acceptance,
            (owner, token) => PrepareAsync(owner, request, member, episodeId, provenance.AcceptedAt, token),
            (TeamMember owner, ImmutableContentRef response) => owner.Assign(provenance, response, episodeId, request.ExpectedAssignmentRevision,
                request.ExpectedAssignmentGeneration)), context, ct).ConfigureAwait(false);
        var result = await TeamMemberResponses.ReadAsync(content, request.TenantId.ToString(), accepted, ct)
            .ConfigureAwait(false);
        if (!result.IsSuccess || accepted.Value is not AcceptedAppendResult.Committed)
            return result;

        return await VerifyMemberEpisodeAsync(context, request, episodeId, result, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<AdministratorTransitionResponse>> AssignAdministratorAsync(
        IRequestContext<AssignTeamMember> context, AssignTeamMember request, Member member,
        OperationProvenance provenance, CancellationToken ct)
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
                return Result<AdministratorTransitionResponse>.Failure(restored.Error!);
        }

        var episodeId = member.MembershipEpisodeId;
        var result = await executor.ExecuteAsync(Owner(request, Uuid.Empty),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.AssignForAdministrators(provenance, episodeId)),
            context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Result<AdministratorTransitionResponse>.Failure(result.Error!);
        var completed = Result<AdministratorTransitionResponse>.Success(new AdministratorTransitionResponse(
            request.CommandId, AdministratorTransitionResponseState.Completed, null, provenance.AcceptedAt));
        return await VerifyMemberEpisodeAsync(context, request, episodeId, completed, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     After the assignment commits the member must still be on the episode it was bound to; otherwise the
    ///     assignment is a retired relationship and is compensated.
    /// </summary>
    async ValueTask<Result<AdministratorTransitionResponse>> VerifyMemberEpisodeAsync(
        IRequestContext<AssignTeamMember> context, AssignTeamMember request, Uuid episodeId,
        Result<AdministratorTransitionResponse> done, CancellationToken ct)
    {
        var currentMember = await reader.HydrateAsync(Member.ForVerification(request.TenantId,
            request.MemberId), ct).ConfigureAwait(false);
        if (currentMember.IsRegistered && currentMember.MembershipEpisodeId == episodeId)
            return done;

        var assignment = await reader.HydrateAsync(Owner(request, Uuid.Empty), ct).ConfigureAwait(false);
        var compensated = await cleanup.ReconcileTeamAssignmentAsync(context, request, episodeId,
            assignment.AssignmentEventId, assignment.AssignmentPosition, ct).ConfigureAwait(false);
        if (!compensated.IsSuccess)
            return Result<AdministratorTransitionResponse>.Failure(compensated.Error!);
        // The assignment was accepted before its episode ended. Cleanup closes that relationship;
        // it must not replace the frozen acceptance with a different response on the first attempt.
        return done;
    }

    async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(TeamMember owner, AssignTeamMember request, Member member,
        Uuid episodeId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        if (!member.IsRegistered)
            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant member was not found."));
        if (member.MembershipEpisodeId != episodeId)
            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The member is on a different membership episode."));
        var prepared = owner.PrepareAssign(episodeId, request.ExpectedAssignmentRevision,
            request.ExpectedAssignmentGeneration);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return Result<FirstResponseDescriptor>.Success(await TeamMemberResponses.FreezeCompletedAsync(content,
            request.TenantId.ToString(), request.CommandId, acceptedAt, ct).ConfigureAwait(false));
    }

    /// <summary>The provenance an assignment carries; the binding of a retry is derived from it.</summary>
    internal static OperationProvenance Provenance(AssignTeamMember request, ActorReference actor,
        DateTimeOffset acceptedAt) =>
        Owner(request, Uuid.Empty).Provenance(request.CommandId, CommandKind, actor,
            request.MemberEpisodeId == Uuid.Empty ? CurrentEpisode : request.MemberEpisodeId.ToString(),
            request.ExpectedAssignmentRevision >= 0 ? (ulong)request.ExpectedAssignmentRevision : null,
            AuthoredIntentDigest.Compute(request), acceptedAt);

    static TeamMember Owner(AssignTeamMember request, Uuid selectedCommandId) =>
        new(request.TenantId, request.TeamId, request.MemberId, selectedCommandId);

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
