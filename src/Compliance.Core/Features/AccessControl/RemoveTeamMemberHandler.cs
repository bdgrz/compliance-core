using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a team member. A non-administrator team is a direct native operation on the team-member owner and
///     never reads the tenant manager guard; removal from the built-in Administrators team first needs the guard to
///     confirm another active administrator remains (#432) until the guard-first owner replaces that branch.
/// </summary>
public sealed class RemoveTeamMemberHandler(IAggregateExecutor executor, NativeOperationExecutor operations,
    TenantManagerInvariant managers, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RemoveTeamMember, AdministratorTransitionResponse>
{
    const string CommandKind = "RemoveTeamMember";
    const string CurrentEpisode = "current";

    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<RemoveTeamMember> context, CancellationToken ct)
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

        var provenance = Provenance(request, actor, clock.GetUtcNow());
        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId))
            return await RemoveAdministratorAsync(context, request, provenance, ct).ConfigureAwait(false);

        var binding = AcceptedOperationBinding.Of(provenance);
        var frozen = await TeamMemberResponses.FreezeCompletedAsync(content, request.TenantId.ToString(),
            request.CommandId, provenance.AcceptedAt, ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TeamMember>(binding,
            () => Owner(request, request.CommandId), owner => owner.Acceptance,
            owner => Prepare(owner, request, frozen),
            (TeamMember owner, ImmutableContentRef response) => owner.Remove(provenance, response, request.MemberEpisodeId,
                request.ExpectedAssignmentRevision, request.ExpectedAssignmentGeneration)), context, ct)
            .ConfigureAwait(false);
        return await TeamMemberResponses.ReadAsync(content, request.TenantId.ToString(), accepted, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<AdministratorTransitionResponse>> RemoveAdministratorAsync(
        IRequestContext<RemoveTeamMember> context, RemoveTeamMember request, OperationProvenance provenance,
        CancellationToken ct)
    {
        Uuid? actorMemberId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId)
            ? RbacIds.Member(request.TenantId, actorUserId)
            : null;
        var guarded = await managers.WithdrawAsync(context, request.TenantId, request.MemberId,
            actorMemberId, TenantManagerInvariant.RemovedFromAdministrators, ct).ConfigureAwait(false);
        if (!guarded.IsSuccess)
            return Result<AdministratorTransitionResponse>.Failure(guarded.Error!);
        var result = await executor.ExecuteAsync(Owner(request, Uuid.Empty),
            teamMember => AggregateOutcome.CommitOnSuccess(teamMember.RemoveForAdministrators(provenance)),
            context, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<AdministratorTransitionResponse>.Success(new AdministratorTransitionResponse(request.CommandId,
                AdministratorTransitionResponseState.Completed, null, provenance.AcceptedAt))
            : Result<AdministratorTransitionResponse>.Failure(result.Error!);
    }

    static Result<FirstResponseDescriptor> Prepare(TeamMember owner, RemoveTeamMember request,
        FirstResponseDescriptor response)
    {
        var prepared = owner.PrepareRemove(request.MemberEpisodeId, request.ExpectedAssignmentRevision,
            request.ExpectedAssignmentGeneration);
        return prepared.IsSuccess
            ? Result<FirstResponseDescriptor>.Success(response)
            : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
    }

    /// <summary>The provenance a removal carries; the binding of a retry is derived from it.</summary>
    internal static OperationProvenance Provenance(RemoveTeamMember request, ActorReference actor,
        DateTimeOffset acceptedAt) =>
        Owner(request, Uuid.Empty).Provenance(request.CommandId, CommandKind, actor,
            request.MemberEpisodeId == Uuid.Empty ? CurrentEpisode : request.MemberEpisodeId.ToString(),
            request.ExpectedAssignmentRevision >= 0 ? (ulong)request.ExpectedAssignmentRevision : null,
            AuthoredIntentDigest.Compute(request), acceptedAt);

    static TeamMember Owner(RemoveTeamMember request, Uuid selectedCommandId) =>
        new(request.TenantId, request.TeamId, request.MemberId, selectedCommandId);

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
