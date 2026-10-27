using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Removes a team member. A non-administrator team is a direct native operation on the team-member owner and
///     never reads the tenant manager guard; removal from the built-in Administrators team is route-determined and
///     appends a guard fact first, withdrawing the member's availability before the administrator transition
///     process removes the assignment.
/// </summary>
public sealed class RemoveTeamMemberHandler(NativeOperationExecutor operations,
    AdministratorTransitionAdmission admissions, IAggregateReader reader, IImmutableContentStore content,
    TimeProvider clock)
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
            return await RemoveAdministratorAsync(context, request, actor, ct).ConfigureAwait(false);

        var binding = AcceptedOperationBinding.Of(provenance);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TeamMember>(binding,
            () => Owner(request, request.CommandId), owner => owner.Acceptance,
            (owner, token) => PrepareAsync(owner, request, provenance.AcceptedAt, token),
            (TeamMember owner, ImmutableContentRef response) => owner.Remove(provenance, response, request.MemberEpisodeId,
                request.ExpectedAssignmentRevision, request.ExpectedAssignmentGeneration)), context, ct)
            .ConfigureAwait(false);
        return await TeamMemberResponses.ReadAsync(content, request.TenantId.ToString(), accepted, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<AdministratorTransitionResponse>> RemoveAdministratorAsync(
        IRequestContext<RemoveTeamMember> context, RemoveTeamMember request, ActorReference actor, CancellationToken ct)
    {
        var assignment = await reader.HydrateAsync(Owner(request, Uuid.Empty), ct).ConfigureAwait(false);
        var episodeId = request.MemberEpisodeId == Uuid.Empty ? assignment.MembershipEpisodeId : request.MemberEpisodeId;
        var admission = new AdministratorAdmission(request.CommandId, request.TenantId, AdministratorCommandKinds.Remove,
            actor, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(), request.MemberId, episodeId,
            assignment.Stream, assignment.Id, request.ExpectedAssignmentRevision, guard =>
            {
                if (episodeId == Uuid.Empty)
                    return Result<AdministratorSourceBinding>.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The member is not on this team."));
                var unfinished = AdministratorAdmissions.Unfinished(guard, episodeId, false, static _ => false);
                if (!unfinished.IsSuccess)
                    return Result<AdministratorSourceBinding>.Failure(unfinished.Error!);
                var prepared = assignment.PrepareRemove(episodeId, request.ExpectedAssignmentRevision,
                    request.ExpectedAssignmentGeneration);
                return prepared.IsSuccess
                    ? Result<AdministratorSourceBinding>.Success(new AdministratorSourceBinding(0,
                        assignment.AssignmentGeneration, assignment.AssignmentRevision, null))
                    : Result<AdministratorSourceBinding>.Failure(prepared.Error!);
            }, ResolveCurrentEpisode: request.MemberEpisodeId == Uuid.Empty);
        return await admissions.AdmitAsync(context, admission, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(TeamMember owner, RemoveTeamMember request,
        DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var prepared = owner.PrepareRemove(request.MemberEpisodeId, request.ExpectedAssignmentRevision,
            request.ExpectedAssignmentGeneration);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return Result<FirstResponseDescriptor>.Success(await TeamMemberResponses.FreezeCompletedAsync(content,
            request.TenantId.ToString(), request.CommandId, acceptedAt, ct).ConfigureAwait(false));
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
