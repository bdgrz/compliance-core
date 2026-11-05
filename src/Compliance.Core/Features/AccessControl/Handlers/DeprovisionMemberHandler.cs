using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Deprovisions directly on Member, where the primary mark and lifecycle are ordered atomically.</summary>
public sealed class DeprovisionMemberHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<DeprovisionMember, AdministratorTransitionResponse>
{
    public ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(IRequestContext<DeprovisionMember> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (AdministratorAdmissions.MemberActor(context.Actor, request.TenantId) is not { } actor)
            return ValueTask.FromResult(Fail(RequestErrorKind.Unauthorized, "Membership changes require a Bdgrz user identity."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty ||
            request.MemberEpisodeId == Uuid.Empty || request.ExpectedMemberRevision < 0)
            return ValueTask.FromResult(Fail(RequestErrorKind.Validation,
                "A command id, tenant, user, member episode and expected member revision are required."));
        if (UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actorUserId) && actorUserId == request.UserId)
            return ValueTask.FromResult(Fail(RequestErrorKind.Conflict, "An administrator cannot deprovision their own tenant membership."));

        return MemberLifecycleOperations.RunAsync(operations, content, clock, context, request.CommandId,
            request.TenantId, request.UserId, request.MemberEpisodeId, request.ExpectedMemberRevision, actor,
            nameof(DeprovisionMember), member => member.PrepareDeprovision(request.MemberEpisodeId, request.ExpectedMemberRevision, request.Reason),
            (member, provenance, intent, frozen) => member.Deprovision(provenance, intent, null,
                Uuid.Parse(actor.Id, System.Globalization.CultureInfo.InvariantCulture), actor.Display, provenance.AcceptedAt, request.Reason, request.MemberEpisodeId,
                request.ExpectedMemberRevision, firstResponse: frozen), ct);
    }

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
